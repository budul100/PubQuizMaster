using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Data;
using PubQuizMaster.Data.Extensions;

namespace PubQuizMaster.Services.Standings
{
    /// <summary>
    /// Team registry: create, rename and merge teams. Names are unique by their normalized form.
    /// </summary>
    public class TeamService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<Team> CreateTeamAsync(string name, CancellationToken ct = default)
        {
            var (trimmed, normalized) = PrepareName(name);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var existing = await FindNameConflictAsync(db, normalized, Guid.Empty, ct);
            if (existing != null)
            {
                throw new InvalidOperationException($"Team '{existing}' already exists.");
            }

            var team = new Team
            {
                Name = trimmed,
                Normalized = normalized,
                CreatedAt = DateTime.UtcNow
            };

            db.Teams.Add(team);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(Constraints.TeamName))
            {
                throw new InvalidOperationException($"Team '{trimmed}' already exists.", ex);
            }

            return team;
        }

        public async Task<Team> GetOrCreateTeamAsync(string name, CancellationToken ct = default)
        {
            var (trimmed, normalized) = PrepareName(name);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var existing = await db.Teams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Normalized == normalized, ct);

            if (existing != null) return existing;

            var team = new Team
            {
                Name = trimmed,
                Normalized = normalized,
                CreatedAt = DateTime.UtcNow
            };

            db.Teams.Add(team);

            try
            {
                await db.SaveChangesAsync(ct);
                return team;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(Constraints.TeamName))
            {
                // Created concurrently by another request, use that one
                return await db.Teams
                    .AsNoTracking()
                    .FirstAsync(t => t.Normalized == normalized, ct);
            }
        }

        public async Task<Team[]> GetTeamsAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Teams
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToArrayAsync(ct);
        }

        /// <summary>
        /// Moves everything of the source team to the target team and deletes the source team.
        /// On conflicts (same answer cell, same legacy result) the target team's entry wins.
        /// </summary>
        public async Task<TeamMerge> MergeTeamsAsync(Guid sourceTeamId, Guid targetTeamId,
            CancellationToken ct = default)
        {
            if (sourceTeamId == targetTeamId)
            {
                throw new InvalidOperationException("A team cannot be merged into itself.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var source = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == sourceTeamId, ct)
                ?? throw new InvalidOperationException("Source team not found.");

            var target = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == targetTeamId, ct)
                ?? throw new InvalidOperationException("Target team not found.");

            // Scorer stations of an open round would keep writing answers for the deleted team
            var inOpenRound = await db.Rounds
                .AnyAsync(r => !r.IsFinalized && r.Assignments.Any(a => a.TeamIds.Contains(sourceTeamId)), ct);

            if (inOpenRound)
            {
                throw new InvalidOperationException(
                    $"Team '{source.Name}' is part of an open round. Finalize the round before merging.");
            }

            // Captured before the participations are moved
            var sourceQuizIds = await db.Participants
                .Where(p => p.TeamId == sourceTeamId)
                .Select(p => p.QuizId)
                .ToArrayAsync(ct);

            var (movedAnswers, droppedAnswers) = await MergeAnswersAsync(db, sourceTeamId, targetTeamId, ct);
            await MergeScorersAsync(db, sourceTeamId, targetTeamId, ct);
            await MergeParticipantsAsync(db, sourceTeamId, targetTeamId, ct);
            var droppedResults = await MergeResultsAsync(db, sourceTeamId, targetTeamId, ct);

            await db.SaveChangesAsync(ct);

            // Totals of completed live nights follow the merged answers
            await ResultService.RebuildAsync(db, sourceQuizIds, ct);

            // Deleted after the moves are saved. Removing the tracked team before would let EF
            // cascade to dependents whose changed TeamId has not been detected yet.
            await db.Teams
                .Where(t => t.Id == sourceTeamId)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);

            return new TeamMerge(source.Name, target.Name, movedAnswers, droppedAnswers, droppedResults);
        }

        public async Task<Team> RenameTeamAsync(Guid teamId, string newName, CancellationToken ct = default)
        {
            var (trimmed, normalized) = PrepareName(newName);

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct)
                ?? throw new InvalidOperationException("Team not found.");

            var conflict = await FindNameConflictAsync(db, normalized, teamId, ct);
            if (conflict != null)
            {
                throw new InvalidOperationException(
                    $"Team '{conflict}' already exists. Merge the teams instead of renaming.");
            }

            team.Name = trimmed;
            team.Normalized = normalized;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(Constraints.TeamName))
            {
                throw new InvalidOperationException($"Team '{trimmed}' already exists.", ex);
            }

            return team;
        }

        #endregion Public Methods

        #region Private Methods

        private static async Task<string?> FindNameConflictAsync(AppDbContext db, string normalized,
            Guid excludedTeamId, CancellationToken ct)
        {
            return await db.Teams
                .AsNoTracking()
                .Where(t => t.Normalized == normalized && t.Id != excludedTeamId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct);
        }

        private static async Task<(int Moved, int Dropped)> MergeAnswersAsync(AppDbContext db,
            Guid sourceTeamId, Guid targetTeamId, CancellationToken ct)
        {
            var sourceAnswers = await db.Answers
                .Where(a => a.TeamId == sourceTeamId)
                .ToArrayAsync(ct);

            if (sourceAnswers.Length == 0) return (0, 0);

            var roundIds = sourceAnswers.Select(a => a.RoundId).Distinct().ToArray();

            var targetCells = (await db.Answers
                    .AsNoTracking()
                    .Where(a => a.TeamId == targetTeamId && roundIds.Contains(a.RoundId))
                    .Select(a => new { a.RoundId, a.QuestionIndex })
                    .ToArrayAsync(ct))
                .Select(c => (c.RoundId, c.QuestionIndex))
                .ToHashSet();

            var moved = 0;
            var dropped = 0;

            foreach (var answer in sourceAnswers)
            {
                if (targetCells.Contains((answer.RoundId, answer.QuestionIndex)))
                {
                    db.Answers.Remove(answer);
                    dropped++;
                }
                else
                {
                    answer.TeamId = targetTeamId;
                    moved++;
                }
            }

            return (moved, dropped);
        }

        private static async Task MergeParticipantsAsync(AppDbContext db, Guid sourceTeamId,
            Guid targetTeamId, CancellationToken ct)
        {
            var participations = await db.Participants
                .Where(p => p.TeamId == sourceTeamId)
                .ToArrayAsync(ct);

            var targetQuizIds = await db.Participants
                .Where(p => p.TeamId == targetTeamId)
                .Select(p => p.QuizId)
                .ToArrayAsync(ct);

            foreach (var participation in participations)
            {
                // TeamId is part of the key, so the row is replaced instead of updated
                if (!targetQuizIds.Contains(participation.QuizId))
                {
                    db.Participants.Add(new Participant
                    {
                        QuizId = participation.QuizId,
                        TeamId = targetTeamId,
                        SheetOrder = participation.SheetOrder
                    });
                }

                db.Participants.Remove(participation);
            }
        }

        private static async Task<int> MergeResultsAsync(AppDbContext db, Guid sourceTeamId,
            Guid targetTeamId, CancellationToken ct)
        {
            // Only legacy results, live results are rebuilt from the merged answers
            var sourceResults = await db.Scores
                .Where(s => s.TeamId == sourceTeamId && s.Quiz.IsLegacyImport)
                .ToArrayAsync(ct);

            var targetQuizIds = await db.Scores
                .Where(s => s.TeamId == targetTeamId && s.Quiz.IsLegacyImport)
                .Select(s => s.QuizId)
                .ToArrayAsync(ct);

            var dropped = 0;

            foreach (var result in sourceResults)
            {
                if (targetQuizIds.Contains(result.QuizId))
                {
                    db.Scores.Remove(result);
                    dropped++;
                }
                else
                {
                    result.TeamId = targetTeamId;
                }
            }

            return dropped;
        }

        private static async Task MergeScorersAsync(AppDbContext db, Guid sourceTeamId,
            Guid targetTeamId, CancellationToken ct)
        {
            var scorers = await db.Scorers
                .Where(s => s.TeamIds.Contains(sourceTeamId))
                .ToArrayAsync(ct);

            foreach (var scorer in scorers)
            {
                // A team may only be assigned to one scorer per round
                var targetAssignedElsewhere = await db.Scorers
                    .AnyAsync(s => s.RoundId == scorer.RoundId
                        && s.Id != scorer.Id
                        && s.TeamIds.Contains(targetTeamId), ct);

                scorer.TeamIds = targetAssignedElsewhere
                    ? [.. scorer.TeamIds.Where(id => id != sourceTeamId)]
                    : ReplaceTeam(scorer.TeamIds, sourceTeamId, targetTeamId);
            }
        }

        private static (string Name, string NormalizedName) PrepareName(string name)
        {
            var trimmed = (name ?? string.Empty).Trim();
            var normalized = MatchingService.Normalize(trimmed);

            if (string.IsNullOrEmpty(normalized))
            {
                throw new InvalidOperationException("Team name must contain at least one letter or digit.");
            }

            return (trimmed, normalized);
        }

        /// <summary>
        /// Replaces the source team in place and keeps the first occurrence if the target is already listed.
        /// </summary>
        private static List<Guid> ReplaceTeam(List<Guid> teamIds, Guid sourceTeamId, Guid targetTeamId)
        {
            return [.. teamIds.Select(id => id == sourceTeamId ? targetTeamId : id).Distinct()];
        }

        #endregion Private Methods
    }
}