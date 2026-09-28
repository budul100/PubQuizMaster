using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Data;
using PubQuizMaster.Data.Extensions;
using PubQuizMaster.Services.Standings;

namespace PubQuizMaster.Services.Event
{
    public class QuizService(IDbContextFactory<AppDbContext> dbFactory, TeamService teamService)
    {
        #region Public Fields

        public const int MaxQuestionCount = 50;

        #endregion Public Fields

        #region Public Methods

        /// <summary>
        /// Makes a planned or completed quiz night the live one. Reopening a completed night
        /// removes its results from the standings until it is completed again.
        /// </summary>
        public async Task ActivateQuizAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            if (quiz.IsLegacyImport)
            {
                throw new InvalidOperationException("Legacy imports cannot be activated.");
            }

            if (quiz.Status == QuizStatus.Live) return;

            var otherLive = await db.Quizzes
                .Where(q => q.Id != quizId && q.Status == QuizStatus.Live)
                .Select(q => q.Title)
                .FirstOrDefaultAsync(ct);

            if (otherLive != null)
            {
                throw new InvalidOperationException(
                    $"Quiz night '{otherLive}' is still live. Complete it before activating another one.");
            }

            var wasCompleted = quiz.Status == QuizStatus.Completed;

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            quiz.Status = QuizStatus.Live;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(Constraints.SingleActiveQuiz))
            {
                throw new InvalidOperationException(
                    "Another quiz night is live. Complete it before activating this one.", ex);
            }

            if (wasCompleted)
            {
                await ResultService.RebuildAsync(db, [quizId], ct);
            }

            await transaction.CommitAsync(ct);
        }

        public async Task<TeamRegistration> AddTeamAsync(Guid quizNightId, string teamName,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .Include(q => q.ParticipatingTeams)
                    .ThenInclude(pt => pt.Team)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Assignments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == quizNightId, ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            if (quiz.Status == QuizStatus.Completed)
            {
                throw new InvalidOperationException($"Quiz night '{quiz.Title}' is already completed.");
            }

            var team = await teamService.GetOrCreateTeamAsync(teamName, ct);

            var existingParticipant = quiz.ParticipatingTeams.FirstOrDefault(pt => pt.TeamId == team.Id);

            if (existingParticipant is { IsActive: true })
            {
                throw new InvalidOperationException(
                    $"Team '{team.Name}' is already registered and active for this night.");
            }

            if (existingParticipant != null)
            {
                existingParticipant.IsActive = true;
            }
            else
            {
                db.Participants.Add(new Participant
                {
                    QuizId = quizNightId,
                    TeamId = team.Id,
                    IsActive = true,
                    IsNonCompetitive = false
                });
            }

            // Reactivated and newly registered teams join a running round the same way
            var (hasOpenRound, scorerLabel) = AssignToOpenRound(quiz, team.Id, team.Name);

            await db.SaveChangesAsync(ct);

            return new TeamRegistration(team.Name, hasOpenRound, scorerLabel);
        }

        public async Task CompleteQuizAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .Include(q => q.Rounds)
                .FirstOrDefaultAsync(q => q.Id == quizId, ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            if (quiz.IsLegacyImport)
            {
                throw new InvalidOperationException("Legacy imports cannot be completed.");
            }

            if (quiz.Status == QuizStatus.Completed) return;

            if (quiz.Status != QuizStatus.Live)
            {
                throw new InvalidOperationException($"Quiz night '{quiz.Title}' has not been started yet.");
            }

            var openRound = quiz.Rounds.FirstOrDefault(r => !r.IsFinalized);
            if (openRound != null)
            {
                throw new InvalidOperationException(
                    $"Finalize round '{openRound.Name}' before completing the quiz night.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // The content is kept: the read-only view and the statistics need the questions
            quiz.Status = QuizStatus.Completed;

            await db.SaveChangesAsync(ct);

            await ResultService.RebuildAsync(db, [quizId], ct);
            await transaction.CommitAsync(ct);
        }

        /// <summary>
        /// Creates a planned quiz night. Any number of nights can be planned,
        /// ActivateQuizAsync turns one of them into the live night.
        /// </summary>
        public async Task<Core.Models.Event.Quiz> CreateQuizAsync(string title, DateOnly date,
            string? description, Core.Models.Content.Quiz? content = null, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = new Core.Models.Event.Quiz
            {
                Title = title.Trim(),
                Date = date,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Content = content,
                Status = QuizStatus.Planned,
                IsLegacyImport = false
            };

            db.Quizzes.Add(quiz);
            await db.SaveChangesAsync(ct);

            return quiz;
        }

        public async Task DeleteQuizAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var deleted = await db.Quizzes
                .Where(q => q.Id == quizId)
                .ExecuteDeleteAsync(ct);

            if (deleted == 0)
            {
                throw new InvalidOperationException("Quiz night not found.");
            }
        }

        public async Task DeleteRoundAsync(Guid roundId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.Rounds
                .Include(r => r.Answers)
                .FirstOrDefaultAsync(r => r.Id == roundId, ct)
                ?? throw new InvalidOperationException("Round not found.");

            if (round.Answers.Count > 0)
            {
                throw new InvalidOperationException("Cannot delete a round with recorded answers.");
            }

            db.Rounds.Remove(round);
            await db.SaveChangesAsync(ct);
        }

        public async Task FinalizeRoundAsync(Guid roundId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.Rounds.FirstOrDefaultAsync(r => r.Id == roundId, ct)
                ?? throw new InvalidOperationException("Round not found.");

            round.IsFinalized = true;
            await db.SaveChangesAsync(ct);
        }

        public async Task<Core.Models.Event.Quiz?> GetActiveQuizAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await WithFullGraph(ActiveQuizzes(db)).FirstOrDefaultAsync(ct);
            return SortRounds(quiz);
        }

        public async Task<QuizFingerprint?> GetActiveQuizFingerprintAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await ActiveQuizzes(db)
                .AsNoTracking()
                .Select(q => new QuizFingerprint(
                    q.Id,
                    q.Title,
                    q.Date,
                    q.Description,
                    q.ParticipatingTeams.Count,
                    q.ParticipatingTeams.Count(p => p.IsActive),
                    q.ParticipatingTeams.Count(p => p.IsNonCompetitive),
                    q.Rounds.Count,
                    q.Rounds.Count(r => r.IsFinalized),
                    q.Rounds.Where(r => r.IsFinal).OrderBy(r => r.CreatedAt).Select(r => (Guid?)r.Id).FirstOrDefault(),
                    q.Rounds.SelectMany(r => r.Assignments).Sum(s => s.TeamIds.Count), q.Rounds.SelectMany(r => r.Answers).Count(),
                    q.Rounds.SelectMany(r => r.Answers).Max(a => (DateTime?)a.RecordedAt)))
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>
        /// Name and scorer stations of the open round of the live quiz night, for the header badges.
        /// Null when no quiz night is live or no round is open.
        /// </summary>
        public async Task<ActiveRound?> GetActiveRoundInfoAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await ActiveQuizzes(db)
                .AsNoTracking()
                .SelectMany(q => q.Rounds)
                .Where(r => !r.IsFinalized)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new { r.Name, ScorerIds = r.Assignments.Select(a => a.ScorerId).ToList() })
                .FirstOrDefaultAsync(ct);

            return round == null
                ? null
                : new ActiveRound(round.Name, [.. round.ScorerIds.Distinct(StringComparer.OrdinalIgnoreCase)]);
        }

        public async Task<List<Core.Models.Event.Quiz>> GetAllQuizzesAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            return await db.Quizzes
                .AsNoTracking()
                .Include(q => q.ParticipatingTeams)
                .Include(q => q.Rounds)
                .OrderByDescending(q => q.Date)
                .ThenByDescending(q => q.Id)
                .ToListAsync(ct);
        }

        public async Task<MatrixData?> GetMatrixDataAsync(Guid roundId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.Rounds
                .AsNoTracking()
                .Include(r => r.Answers)
                .FirstOrDefaultAsync(r => r.Id == roundId, ct);

            if (round == null) return null;

            // A team deactivated mid-round keeps its cells editable as long as it has answers here
            var answeringTeamIds = round.Answers
                .Select(a => a.TeamId)
                .Distinct()
                .ToArray();

            var participants = await db.Participants
                .AsNoTracking()
                .Where(p => p.QuizId == round.QuizId
                    && (p.IsActive || answeringTeamIds.Contains(p.TeamId)))
                .Select(p => new MatrixTeam(p.TeamId, p.Team.Name, p.IsNonCompetitive))
                .ToArrayAsync(ct);

            // Sorted in memory: the database collation does not match the sheet order
            var teams = participants
                .OrderBy(t => t.Name, TeamNameComparer.Instance)
                .ToArray();

            var previousAnswers = await db.Rounds
                .AsNoTracking()
                .Where(r => r.QuizId == round.QuizId && r.CreatedAt < round.CreatedAt)
                .SelectMany(r => r.Answers.Select(a => new { a.TeamId, a.Value }))
                .ToArrayAsync(ct);

            var priorScores = previousAnswers
                .GroupBy(a => a.TeamId)
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Value.GetScore()));

            return new MatrixData(round, teams, priorScores);
        }

        public async Task<Core.Models.Event.Quiz?> GetQuizAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await WithFullGraph(db.Quizzes).FirstOrDefaultAsync(q => q.Id == quizId, ct);
            return SortRounds(quiz);
        }

        public async Task<StateDto> GetStateAsync(string scorerId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var activeQuizId = await ActiveQuizzes(db)
                .Select(q => (Guid?)q.Id)
                .FirstOrDefaultAsync(ct);

            if (activeQuizId == null)
            {
                return new StateDto(null, null, [], []);
            }

            var openRound = await db.Rounds
                .AsNoTracking()
                .Include(r => r.Assignments)
                .Where(r => r.QuizId == activeQuizId && !r.IsFinalized)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (openRound == null)
            {
                return new StateDto(null, null, [], []);
            }

            var assignment = openRound.Assignments.FirstOrDefault(a => a.ScorerId == scorerId);
            if (assignment == null)
            {
                return new StateDto(null, openRound, [], []);
            }

            var teamIds = assignment.TeamIds.ToArray();

            var teams = await db.Teams
                .AsNoTracking()
                .Where(t => teamIds.Contains(t.Id))
                .ToArrayAsync(ct);

            // The sheet stack is strictly alphabetical, whatever order the assignment was stored in
            var orderedTeams = teams
                .OrderBy(t => t.Name, TeamNameComparer.Instance)
                .ToList();

            var answers = await db.Answers
                .AsNoTracking()
                .Where(a => a.RoundId == openRound.Id && teamIds.Contains(a.TeamId))
                .ToListAsync(ct);

            var content = openRound.Position == null
                ? null
                : await db.Quizzes
                    .AsNoTracking()
                    .Where(q => q.Id == openRound.QuizId)
                    .Select(q => q.Content)
                    .FirstOrDefaultAsync(ct);

            return new StateDto(assignment, openRound, orderedTeams, answers,
                content?.GetRound(openRound.Position));
        }

        public async Task RecordAnswerAsync(Guid roundId, Guid teamId, int questionIndex, bool isCorrect,
                              string scorerId, CancellationToken ct = default)
        {
            await RetryOnAnswerCellConflictAsync(
                () => RecordAnswerCoreAsync(roundId, teamId, questionIndex, isCorrect, scorerId, ct));
        }

        public async Task RemoveTeamAsync(Guid quizId, Guid teamId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var hasAnswers = await db.Rounds
                .Where(r => r.QuizId == quizId)
                .AnyAsync(r => r.Answers.Any(a => a.TeamId == teamId), ct);

            if (hasAnswers)
            {
                throw new InvalidOperationException("Cannot delete a team with recorded answers. Deactivate it instead.");
            }

            var participant = await db.Participants
                .FirstOrDefaultAsync(p => p.QuizId == quizId && p.TeamId == teamId, ct)
                ?? throw new InvalidOperationException("Team participation not found.");

            db.Participants.Remove(participant);

            var roundIds = await db.Rounds
                .Where(r => r.QuizId == quizId)
                .Select(r => r.Id)
                .ToArrayAsync(ct);

            var scorers = await db.Scorers
                .Where(s => roundIds.Contains(s.RoundId) && s.TeamIds.Contains(teamId))
                .ToArrayAsync(ct);

            foreach (var scorer in scorers)
            {
                scorer.TeamIds = [.. scorer.TeamIds.Where(id => id != teamId)];
            }

            await db.SaveChangesAsync(ct);

            var hasOtherReferences = await db.Participants.AnyAsync(p => p.TeamId == teamId, ct)
                || await db.Answers.AnyAsync(a => a.TeamId == teamId, ct)
                || await db.Scores.AnyAsync(s => s.TeamId == teamId, ct);

            if (!hasOtherReferences)
            {
                await db.Teams.Where(t => t.Id == teamId).ExecuteDeleteAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }

        /// <summary>
        /// Renames a round. Same rules as when starting it; returns the stored (trimmed) name.
        /// </summary>
        public async Task<string> RenameRoundAsync(Guid roundId, string newName, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.Rounds.FirstOrDefaultAsync(r => r.Id == roundId, ct)
                ?? throw new InvalidOperationException("Round not found.");

            var quiz = await db.Quizzes
                .AsNoTracking()
                .Where(q => q.Id == round.QuizId)
                .Select(q => new { q.Title, q.Status })
                .FirstAsync(ct);

            if (quiz.Status == QuizStatus.Completed)
            {
                throw new InvalidOperationException($"Quiz night '{quiz.Title}' is already completed.");
            }

            var otherNames = await db.Rounds
                .Where(r => r.QuizId == round.QuizId && r.Id != roundId)
                .Select(r => r.Name)
                .ToArrayAsync(ct);

            var roundName = ValidateRoundName(newName, otherNames);

            if (round.Name != roundName)
            {
                round.Name = roundName;
                await db.SaveChangesAsync(ct);
            }

            return roundName;
        }

        public async Task SetFinalRoundAsync(Guid roundId, bool isFinal, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.Rounds.FirstOrDefaultAsync(r => r.Id == roundId, ct)
                ?? throw new InvalidOperationException("Round not found.");

            if (isFinal)
            {
                await ClearOtherFinalRoundsAsync(db, round.QuizId, roundId, ct);
            }

            round.IsFinal = isFinal;
            await db.SaveChangesAsync(ct);
        }

        public async Task SetParticipantStatusAsync(Guid quizId, Guid teamId, bool isActive, bool isNonCompetitive,
                              CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var participant = await db.Participants
                .FirstOrDefaultAsync(p => p.QuizId == quizId && p.TeamId == teamId, ct)
                ?? throw new InvalidOperationException("Team participation not found.");

            participant.IsActive = isActive;
            participant.IsNonCompetitive = isNonCompetitive;

            if (!isActive)
            {
                var openRound = await db.Rounds
                    .Include(r => r.Assignments)
                    .Include(r => r.Answers)
                    .Where(r => r.QuizId == quizId && !r.IsFinalized)
                    .FirstOrDefaultAsync(ct);

                if (openRound != null && !openRound.Answers.Any(a => a.TeamId == teamId))
                {
                    foreach (var sc in openRound.Assignments.Where(s => s.TeamIds.Contains(teamId)))
                    {
                        sc.TeamIds = [.. sc.TeamIds.Where(id => id != teamId)];
                    }
                }
            }

            await db.SaveChangesAsync(ct);
        }

        public async Task<Core.Models.Event.Round> StartRoundAsync(RoundRequest request, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .AsNoTracking()
                .Include(q => q.ParticipatingTeams)
                    .ThenInclude(pt => pt.Team)
                .Include(q => q.Rounds)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == request.QuizId, ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            ValidateRoundRequest(request, quiz);

            if (request.IsFinal)
            {
                await ClearOtherFinalRoundsAsync(db, request.QuizId, null, ct);
            }

            var roundId = Guid.NewGuid();

            var newRound = new Core.Models.Event.Round
            {
                Id = roundId,
                QuizId = request.QuizId,
                Name = request.RoundName.Trim(),
                Length = request.Length,
                IsFinal = request.IsFinal,
                IsFinalized = false,
                Position = request.ContentPosition,
                CreatedAt = DateTime.UtcNow
            };

            var teamNames = quiz.ParticipatingTeams.ToDictionary(p => p.TeamId, p => p.Team.Name);

            foreach (var assign in request.Assignments)
            {
                newRound.Assignments.Add(new Scorer
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    ScorerId = assign.ScorerId.Trim(),
                    Label = assign.Label.Trim(),
                    TeamIds = OrderTeamIds(assign.TeamIds, teamNames)
                });
            }

            db.Rounds.Add(newRound);

            await SyncStationsAsync(db, request, ct);
            await db.SaveChangesAsync(ct);

            return newRound;
        }

        public async Task UpdateAnswersAsync(Guid roundId,
                              Dictionary<(Guid TeamId, int QuestionIndex), bool> cellUpdates, CancellationToken ct = default)
        {
            if (cellUpdates.Count == 0) return;

            await RetryOnAnswerCellConflictAsync(
                () => UpdateAnswersCoreAsync(roundId, cellUpdates, ct));
        }

        public async Task UpdateQuizAsync(QuizDetails update, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(update.Title))
            {
                throw new InvalidOperationException("Title is required.");
            }

            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == update.QuizId, ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            quiz.Title = update.Title.Trim();
            quiz.Date = update.Date;
            quiz.Description = string.IsNullOrWhiteSpace(update.Description) ? null : update.Description.Trim();
            quiz.Content = update.Content;

            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods

        #region Private Methods

        private static IQueryable<Core.Models.Event.Quiz> ActiveQuizzes(AppDbContext db)
        {
            return db.Quizzes
                .Where(q => q.Status == QuizStatus.Live)
                .OrderByDescending(q => q.Date)
                .ThenByDescending(q => q.Id);
        }

        /// <summary>
        /// Adds the team to the open round, if there is one: to the station whose alphabetical range
        /// covers the name, at its alphabetical position in the stack. A team that is already assigned
        /// keeps its station. Requires a tracked quiz graph with Participants (incl. Team), Rounds and
        /// Assignments loaded.
        /// </summary>
        private static (bool HasOpenRound, string? ScorerLabel) AssignToOpenRound(Core.Models.Event.Quiz quiz,
            Guid teamId, string teamName)
        {
            var openRound = quiz.Rounds
                .OrderBy(r => r.CreatedAt)
                .LastOrDefault(r => !r.IsFinalized);

            if (openRound == null) return (false, null);

            var scorer = openRound.Assignments.FirstOrDefault(a => a.TeamIds.Contains(teamId));

            if (scorer == null)
            {
                var stations = openRound.Assignments
                    .OrderBy(a => a.Label, TeamNameComparer.Instance)
                    .ToArray();

                if (stations.Length == 0) return (true, null);

                // A participant added in this context is fixed up into the list without its team
                var teamNames = quiz.ParticipatingTeams
                    .Where(p => p.Team != null)
                    .ToDictionary(p => p.TeamId, p => p.Team.Name);

                teamNames[teamId] = teamName;

                var stationNames = stations
                    .Select(s => s.TeamIds
                        .Select(id => teamNames.GetValueOrDefault(id))
                        .OfType<string>()
                        .ToArray())
                    .ToArray();

                scorer = stations[TeamDistribution.FindStation(stationNames, teamName)];
                scorer.TeamIds = OrderTeamIds([.. scorer.TeamIds, teamId], teamNames);
            }

            return (true, string.IsNullOrWhiteSpace(scorer.Label) ? scorer.ScorerId : scorer.Label);
        }

        /// <summary>
        /// Resets IsFinal on all rounds of the quiz except the given one. Only one final round per quiz.
        /// Changes are tracked; the caller saves them together with its own changes.
        /// </summary>
        private static async Task ClearOtherFinalRoundsAsync(AppDbContext db, Guid quizId, Guid? exceptRoundId,
            CancellationToken ct)
        {
            var others = await db.Rounds
                .Where(r => r.QuizId == quizId && r.IsFinal && r.Id != exceptRoundId)
                .ToArrayAsync(ct);

            foreach (var other in others)
            {
                other.IsFinal = false;
            }
        }

        /// <summary>Team ids in sheet order, i.e. alphabetical by team name.</summary>
        private static List<Guid> OrderTeamIds(IEnumerable<Guid> teamIds, IReadOnlyDictionary<Guid, string> teamNames)
        {
            return [.. teamIds
                .Distinct()
                .OrderBy(id => teamNames.GetValueOrDefault(id, string.Empty), TeamNameComparer.Instance)];
        }

        private static async Task RetryOnAnswerCellConflictAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(Constraints.AnswerCell))
            {
                await action();
            }
        }

        private static async Task SaveAnswersAsync(AppDbContext db, CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entries = string.Join(", ", ex.Entries.Select(e => $"{e.Metadata.ClrType.Name} ({e.State})"));
                throw new InvalidOperationException($"Concurrency conflict on: {entries}", ex);
            }
        }

        private static Core.Models.Event.Quiz? SortRounds(Core.Models.Event.Quiz? quiz)
        {
            if (quiz != null)
            {
                quiz.Rounds = [.. quiz.Rounds.OrderBy(r => r.CreatedAt)];
                quiz.Stations = [.. quiz.Stations.OrderBy(s => s.Label, TeamNameComparer.Instance)];
            }

            return quiz;
        }

        /// <summary>
        /// The stations of the night follow the round just started: labels are updated,
        /// stations added in the start dialog are created, removed ones are deleted.
        /// Changes are tracked; the caller saves them together with the round.
        /// </summary>
        private static async Task SyncStationsAsync(AppDbContext db, RoundRequest request, CancellationToken ct)
        {
            var stations = await db.ScorerStations
                .Where(s => s.QuizId == request.QuizId)
                .ToArrayAsync(ct);

            // Duplicate tokens are rejected by ValidateRoundRequest
            var requested = request.Assignments.ToDictionary(
                keySelector: a => a.ScorerId.Trim(),
                elementSelector: a => a.Label.Trim(),
                comparer: StringComparer.OrdinalIgnoreCase);

            foreach (var station in stations)
            {
                if (requested.Remove(station.ScorerId, out var label))
                {
                    station.Label = label;
                }
                else
                {
                    db.ScorerStations.Remove(station);
                }
            }

            foreach (var (scorerId, label) in requested)
            {
                db.ScorerStations.Add(new ScorerStation
                {
                    QuizId = request.QuizId,
                    ScorerId = scorerId,
                    Label = label
                });
            }
        }

        /// <summary>
        /// Round names must be set and unique per quiz night (case-insensitive):
        /// the presentation export finds the round's slides by a section of the same name.
        /// </summary>
        private static string ValidateRoundName(string name, IEnumerable<string> otherRoundNames)
        {
            var roundName = name.Trim();
            if (roundName.Length == 0)
            {
                throw new InvalidOperationException("Round name is required.");
            }

            if (otherRoundNames.Any(n => n.Equals(roundName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Round \"{roundName}\" already exists.");
            }

            return roundName;
        }

        private static void ValidateRoundRequest(RoundRequest request, Core.Models.Event.Quiz quiz)
        {
            if (quiz.Status == QuizStatus.Completed)
            {
                throw new InvalidOperationException($"Quiz night '{quiz.Title}' is already completed.");
            }

            if (quiz.Status != QuizStatus.Live)
            {
                throw new InvalidOperationException($"Activate quiz night '{quiz.Title}' before starting a round.");
            }

            var openRound = quiz.Rounds.FirstOrDefault(r => !r.IsFinalized);
            if (openRound != null)
            {
                throw new InvalidOperationException($"Finalize round '{openRound.Name}' before starting a new one.");
            }

            ValidateRoundName(request.RoundName, quiz.Rounds.Select(r => r.Name));

            if (request.Length is < 1 or > MaxQuestionCount)
            {
                throw new InvalidOperationException($"Question count must be between 1 and {MaxQuestionCount}.");
            }

            if (request.ContentPosition is int position)
            {
                var contentRound = quiz.Content?.GetRound(position)
                    ?? throw new InvalidOperationException($"Round {position} is not part of the uploaded questions.");

                if (contentRound.Questions.Count != request.Length)
                {
                    throw new InvalidOperationException(
                        $"Round {position} of the uploaded questions has {contentRound.Questions.Count} questions, " +
                        $"the round is set up for {request.Length}.");
                }
            }

            if (request.Assignments.Count == 0)
            {
                throw new InvalidOperationException("At least one scorer station is required.");
            }

            if (request.Assignments.Any(a => string.IsNullOrWhiteSpace(a.ScorerId)))
            {
                throw new InvalidOperationException("Every scorer needs a Scorer ID.");
            }

            var duplicateIds = request.Assignments
                .GroupBy(a => a.ScorerId.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToArray();

            if (duplicateIds.Length > 0)
            {
                throw new InvalidOperationException($"Scorer IDs must be unique: {string.Join(", ", duplicateIds)}");
            }

            var assignedTeamIds = request.Assignments.SelectMany(a => a.TeamIds).ToArray();
            var activeParticipantIds = quiz.ParticipatingTeams
                .Where(pt => pt.IsActive)
                .Select(pt => pt.TeamId)
                .ToHashSet();

            if (assignedTeamIds.Length != activeParticipantIds.Count || !activeParticipantIds.SetEquals(assignedTeamIds))
            {
                throw new InvalidOperationException(
                    $"All {activeParticipantIds.Count} active teams must be assigned to exactly one scorer station.");
            }
        }

        private static IQueryable<Core.Models.Event.Quiz> WithFullGraph(IQueryable<Core.Models.Event.Quiz> query)
        {
            return query
                .AsNoTracking()
                .Include(q => q.ParticipatingTeams)
                    .ThenInclude(pt => pt.Team)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Assignments)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Answers)
                .Include(q => q.Stations)
                .AsSplitQuery();
        }

        private async Task RecordAnswerCoreAsync(Guid roundId, Guid teamId, int questionIndex, bool isCorrect,
                                                      string scorerId, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var round = await db.Rounds
                .AsNoTracking()
                .Where(r => r.Id == roundId)
                .Select(r => new { r.Name, r.IsFinalized })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Round not found.");

            if (round.IsFinalized)
            {
                throw new InvalidOperationException($"Round '{round.Name}' is already finalized.");
            }

            var existing = await db.Answers
                .FirstOrDefaultAsync(a =>
                    a.RoundId == roundId
                    && a.TeamId == teamId
                    && a.QuestionIndex == questionIndex, ct);

            if (existing != null)
            {
                existing.Value = new AnswerBool { Correct = isCorrect };
                existing.ScorerId = scorerId;
                existing.RecordedAt = DateTime.UtcNow;
            }
            else
            {
                db.Answers.Add(new Answer
                {
                    RoundId = roundId,
                    TeamId = teamId,
                    QuestionIndex = questionIndex,
                    Value = new AnswerBool { Correct = isCorrect },
                    ScorerId = scorerId,
                    RecordedAt = DateTime.UtcNow
                });
            }

            await SaveAnswersAsync(db, ct);
        }

        private async Task UpdateAnswersCoreAsync(Guid roundId,
            Dictionary<(Guid TeamId, int QuestionIndex), bool> cellUpdates, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .AsNoTracking()
                .Where(q => q.Rounds.Any(r => r.Id == roundId))
                .Select(q => new { q.Id, q.Status })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Round not found.");

            var isCompleted = quiz.Status == QuizStatus.Completed;

            await using var transaction = isCompleted
                ? await db.Database.BeginTransactionAsync(ct)
                : null;

            var teamIds = cellUpdates.Keys
                .Select(k => k.TeamId)
                .Distinct()
                .ToArray();

            var lookup = await db.Answers
                .Where(a => a.RoundId == roundId && teamIds.Contains(a.TeamId))
                .ToDictionaryAsync(a => (a.TeamId, a.QuestionIndex), ct);

            foreach (var (key, value) in cellUpdates)
            {
                if (lookup.TryGetValue(key, out var existing))
                {
                    existing.Value = new AnswerBool { Correct = value };
                    existing.ScorerId = "host";
                    existing.RecordedAt = DateTime.UtcNow;
                }
                else
                {
                    db.Answers.Add(new Answer
                    {
                        RoundId = roundId,
                        TeamId = key.TeamId,
                        QuestionIndex = key.QuestionIndex,
                        Value = new AnswerBool { Correct = value },
                        ScorerId = "host",
                        RecordedAt = DateTime.UtcNow
                    });
                }
            }

            await SaveAnswersAsync(db, ct);

            if (transaction != null)
            {
                await ResultService.RebuildAsync(db, [quiz.Id], ct);
                await transaction.CommitAsync(ct);
            }
        }

        #endregion Private Methods
    }
}
