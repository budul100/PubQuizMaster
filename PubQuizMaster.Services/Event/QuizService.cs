using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Player;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Data;
using PubQuizMaster.Services.Player;

namespace PubQuizMaster.Services.Event
{
    public class QuizService(IDbContextFactory<AppDbContext> dbFactory, TeamService teamService)
    {
        #region Public Fields

        public const int MaxQuestionCount = 50;

        #endregion Public Fields

        #region Public Methods

        public async Task<TeamRegistration> AddTeamAsync(Guid quizNightId, string teamName,
                     CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .Include(q => q.ParticipatingTeams)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Assignments)
                .AsSplitQuery()
                .FirstOrDefaultAsync(q => q.Id == quizNightId, ct)
                ?? throw new InvalidOperationException("Active quiz night not found.");

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
                var nextSheetOrder = quiz.ParticipatingTeams.Count > 0
                    ? quiz.ParticipatingTeams.Max(pt => pt.SheetOrder) + 1
                    : 1;

                db.Participants.Add(new Participant
                {
                    QuizId = quizNightId,
                    TeamId = team.Id,
                    SheetOrder = nextSheetOrder,
                    IsActive = true,
                    IsNonCompetitive = false
                });
            }

            // Reactivated and newly registered teams join a running round the same way
            var (hasOpenRound, scorerLabel) = AssignToOpenRound(quiz, team.Id);

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

            if (quiz.IsCompleted) return;

            var openRound = quiz.Rounds.FirstOrDefault(r => !r.IsFinalized);
            if (openRound != null)
            {
                throw new InvalidOperationException(
                    $"Finalize round '{openRound.Name}' before completing the quiz night.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            quiz.IsCompleted = true;
            await db.SaveChangesAsync(ct);

            await LiveResultBuilder.RebuildAsync(db, [quizId], ct);
            await transaction.CommitAsync(ct);
        }

        public async Task<Quiz> CreateQuizAsync(string title, DateOnly date, string? description,
                     CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var hasActive = await db.Quizzes.AnyAsync(q => !q.IsCompleted && !q.IsLegacyImport, ct);
            if (hasActive)
            {
                throw new InvalidOperationException(
                    "An active quiz night is already in progress. " +
                    "Complete or delete it before creating a new one.");
            }

            var quiz = new Quiz
            {
                Title = title.Trim(),
                Date = date,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                IsCompleted = false,
                IsLegacyImport = false
            };

            db.Quizzes.Add(quiz);

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(DbConstraintNames.SingleActiveQuiz))
            {
                throw new InvalidOperationException(
                    "An active quiz night is already in progress. " +
                    "Complete or delete it before creating a new one.", ex);
            }

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

        public async Task<Quiz?> GetActiveQuizAsync(CancellationToken ct = default)
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
        /// Name and scorer stations of the open round of the active quiz night, for the header badges.
        /// Null when no quiz night is active or no round is open.
        /// </summary>
        public async Task<ActiveRoundInfo?> GetActiveRoundInfoAsync(CancellationToken ct = default)
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
                : new ActiveRoundInfo(round.Name, [.. round.ScorerIds.Distinct(StringComparer.OrdinalIgnoreCase)]);
        }

        public async Task<List<Quiz>> GetAllQuizzesAsync(CancellationToken ct = default)
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

            var teams = await db.Participants
                .AsNoTracking()
                .Where(p => p.QuizId == round.QuizId
                    && (p.IsActive || answeringTeamIds.Contains(p.TeamId)))
                .OrderBy(p => p.SheetOrder)
                .Select(p => new MatrixTeam(p.TeamId, p.Team.Name, p.IsNonCompetitive))
                .ToArrayAsync(ct);

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

        public async Task<Quiz?> GetQuizAsync(Guid quizId, CancellationToken ct = default)
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

            var orderedTeams = teamIds
                .Select(id => teams.FirstOrDefault(t => t.Id == id))
                .OfType<Team>()
                .ToList();

            var answers = await db.Answers
                .AsNoTracking()
                .Where(a => a.RoundId == openRound.Id && teamIds.Contains(a.TeamId))
                .ToListAsync(ct);

            return new StateDto(assignment, openRound, orderedTeams, answers);
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
                .Select(q => new { q.Title, q.IsCompleted })
                .FirstAsync(ct);

            if (quiz.IsCompleted)
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

        public async Task ReopenQuizAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes.FirstOrDefaultAsync(q => q.Id == quizId, ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            if (quiz.IsLegacyImport)
            {
                throw new InvalidOperationException("Legacy imports cannot be reopened.");
            }

            if (!quiz.IsCompleted) return;

            var otherActive = await db.Quizzes
                .Where(q => q.Id != quizId && !q.IsCompleted && !q.IsLegacyImport)
                .Select(q => q.Title)
                .FirstOrDefaultAsync(ct);

            if (otherActive != null)
            {
                throw new InvalidOperationException(
                    $"Quiz night '{otherActive}' is still active. Complete it before reopening another one.");
            }

            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            quiz.IsCompleted = false;

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(DbConstraintNames.SingleActiveQuiz))
            {
                throw new InvalidOperationException(
                    "Another quiz night is active. Complete it before reopening this one.", ex);
            }

            await LiveResultBuilder.RebuildAsync(db, [quizId], ct);
            await transaction.CommitAsync(ct);
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

        public async Task<Round> StartRoundAsync(RoundRequest request, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .AsNoTracking()
                .Include(q => q.ParticipatingTeams)
                .Include(q => q.Rounds)
                .FirstOrDefaultAsync(q => q.Id == request.QuizNightId, ct)
                ?? throw new InvalidOperationException("Active quiz night not found.");

            ValidateRoundRequest(request, quiz);

            if (request.IsFinal)
            {
                await ClearOtherFinalRoundsAsync(db, request.QuizNightId, null, ct);
            }

            var roundId = Guid.NewGuid();

            var newRound = new Round
            {
                Id = roundId,
                QuizId = request.QuizNightId,
                Name = request.RoundName.Trim(),
                QuestionCount = request.QuestionCount,
                IsFinal = request.IsFinal,
                IsFinalized = false,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var assign in request.Assignments)
            {
                newRound.Assignments.Add(new Scorer
                {
                    Id = Guid.NewGuid(),
                    RoundId = roundId,
                    ScorerId = assign.ScorerId.Trim(),
                    Label = assign.Label.Trim(),
                    TeamIds = assign.TeamIds
                });
            }

            db.Rounds.Add(newRound);
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

        public async Task UpdateQuizAsync(QuizDetailsUpdate update, CancellationToken ct = default)
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

            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods

        #region Private Methods

        private static IQueryable<Quiz> ActiveQuizzes(AppDbContext db)
        {
            return db.Quizzes
                .Where(q => !q.IsCompleted && !q.IsLegacyImport)
                .OrderByDescending(q => q.Date)
                .ThenByDescending(q => q.Id);
        }

        /// <summary>
        /// Adds the team to the scorer with the fewest sheets of the open round, if there is one.
        /// A team that is already assigned keeps its station. Requires a tracked quiz graph
        /// with Rounds and Assignments loaded.
        /// </summary>
        private static (bool HasOpenRound, string? ScorerLabel) AssignToOpenRound(Quiz quiz, Guid teamId)
        {
            var openRound = quiz.Rounds
                .OrderBy(r => r.CreatedAt)
                .LastOrDefault(r => !r.IsFinalized);

            if (openRound == null) return (false, null);

            var scorer = openRound.Assignments.FirstOrDefault(a => a.TeamIds.Contains(teamId));

            if (scorer == null)
            {
                scorer = openRound.Assignments
                    .OrderBy(a => a.TeamIds.Count)
                    .ThenBy(a => a.Label)
                    .FirstOrDefault();

                if (scorer == null) return (true, null);

                scorer.TeamIds = [.. scorer.TeamIds, teamId];
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

        private static async Task RetryOnAnswerCellConflictAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(DbConstraintNames.AnswerCell))
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

        private static Quiz? SortRounds(Quiz? quiz)
        {
            if (quiz != null)
            {
                quiz.Rounds = [.. quiz.Rounds.OrderBy(r => r.CreatedAt)];
            }

            return quiz;
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

        private static void ValidateRoundRequest(RoundRequest request, Quiz quiz)
        {
            if (quiz.IsCompleted)
            {
                throw new InvalidOperationException($"Quiz night '{quiz.Title}' is already completed.");
            }

            var openRound = quiz.Rounds.FirstOrDefault(r => !r.IsFinalized);
            if (openRound != null)
            {
                throw new InvalidOperationException($"Finalize round '{openRound.Name}' before starting a new one.");
            }

            ValidateRoundName(request.RoundName, quiz.Rounds.Select(r => r.Name));

            if (request.QuestionCount is < 1 or > MaxQuestionCount)
            {
                throw new InvalidOperationException($"Question count must be between 1 and {MaxQuestionCount}.");
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

        private static IQueryable<Quiz> WithFullGraph(IQueryable<Quiz> query)
        {
            return query
                .AsNoTracking()
                .Include(q => q.ParticipatingTeams)
                    .ThenInclude(pt => pt.Team)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Assignments)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Answers)
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
                existing.RecordedByScorerId = scorerId;
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
                    RecordedByScorerId = scorerId,
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
                .Select(q => new { q.Id, q.IsCompleted })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Round not found.");

            await using var transaction = quiz.IsCompleted
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
                    existing.RecordedByScorerId = "host";
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
                        RecordedByScorerId = "host",
                        RecordedAt = DateTime.UtcNow
                    });
                }
            }

            await SaveAnswersAsync(db, ct);

            if (transaction != null)
            {
                await LiveResultBuilder.RebuildAsync(db, [quiz.Id], ct);
                await transaction.CommitAsync(ct);
            }
        }

        #endregion Private Methods
    }
}
