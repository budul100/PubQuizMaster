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

            if (quiz.ParticipatingTeams.Any(pt => pt.TeamId == team.Id))
            {
                throw new InvalidOperationException(
                    $"Team '{team.Name}' is already registered for this night.");
            }

            var nextSheetOrder = quiz.ParticipatingTeams.Count > 0
                ? quiz.ParticipatingTeams.Max(pt => pt.SheetOrder) + 1
                : 1;

            var participant = new Participant
            {
                QuizId = quizNightId,
                TeamId = team.Id,
                SheetOrder = nextSheetOrder
            };
            db.Participants.Add(participant);

            var openRound = quiz.Rounds
                .OrderBy(r => r.CreatedAt)
                .LastOrDefault(r => !r.IsFinalized);

            string? scorerLabel = null;

            if (openRound != null)
            {
                // The team is new to this night, so no scorer has it yet. Appended at the end,
                // so the positions of a station that is already scoring stay valid.
                var scorer = openRound.Assignments
                    .OrderBy(a => a.TeamIds.Count)
                    .ThenBy(a => a.Label)
                    .FirstOrDefault();

                if (scorer != null)
                {
                    // New list instance so change tracking sees the modification in any case
                    scorer.TeamIds = [.. scorer.TeamIds, team.Id];
                    scorerLabel = string.IsNullOrWhiteSpace(scorer.Label) ? scorer.ScorerId : scorer.Label;
                }
            }

            await db.SaveChangesAsync(ct);

            return new TeamRegistration(team.Name, openRound != null, scorerLabel);
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
                // Another request created a live quiz night after the check above
                throw new InvalidOperationException(
                    "An active quiz night is already in progress. " +
                    "Complete or delete it before creating a new one.", ex);
            }

            return quiz;
        }

        public async Task DeleteQuizAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Rounds, scorers, answers, participants and legacy scores follow via FK cascade
            var deleted = await db.Quizzes
                .Where(q => q.Id == quizId)
                .ExecuteDeleteAsync(ct);

            if (deleted == 0)
            {
                throw new InvalidOperationException("Quiz night not found.");
            }
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

            // Single query with scalar subqueries, no entities are materialized
            return await ActiveQuizzes(db)
                .AsNoTracking()
                .Select(q => new QuizFingerprint(
                    q.Id,
                    q.Title,
                    q.Date,
                    q.Description,
                    q.ParticipatingTeams.Count,
                    q.Rounds.Count,
                    q.Rounds.Count(r => r.IsFinalized),
                    q.Rounds.SelectMany(r => r.Assignments).Sum(s => s.TeamIds.Count),
                    q.Rounds.SelectMany(r => r.Answers).Count(),
                    q.Rounds.SelectMany(r => r.Answers).Max(a => (DateTime?)a.RecordedAt)))
                .FirstOrDefaultAsync(ct);
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

            // Teams of the round's own quiz night, not of whichever night is active
            var teams = await db.Participants
                .AsNoTracking()
                .Where(p => p.QuizId == round.QuizId)
                .OrderBy(p => p.SheetOrder)
                .Select(p => p.Team)
                .ToArrayAsync(ct);

            // Only the answers of earlier rounds, the score itself is only available in memory (jsonb)
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

            // Only the open round and this station's slice of it, not the whole quiz night
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

            // Sheet order as defined by the assignment
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

            // Only one live quiz night at a time
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
                // Another quiz night became active after the check above
                throw new InvalidOperationException(
                    "Another quiz night is active. Complete it before reopening this one.", ex);
            }

            // Materialized totals are rebuilt when the night is completed again
            await LiveResultBuilder.RebuildAsync(db, [quizId], ct);
            await transaction.CommitAsync(ct);
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

            var roundId = Guid.NewGuid();

            var newRound = new Round
            {
                Id = roundId,
                QuizId = request.QuizNightId,
                Name = request.RoundName.Trim(),
                QuestionCount = request.QuestionCount,
                IsFinalized = false,
                CreatedAt = DateTime.UtcNow
            };

            // Scorers without teams are kept: the station stays linked to its token and
            // AddTeamAsync assigns late registrations to it first
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
            Dictionary<(Guid TeamId, int QuestionIndex), bool?> cellUpdates, CancellationToken ct = default)
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
            // At most one row thanks to IX_Quizzes_SingleActive, the ordering is only a safeguard
            return db.Quizzes
                .Where(q => !q.IsCompleted && !q.IsLegacyImport)
                .OrderByDescending(q => q.Date)
                .ThenByDescending(q => q.Id);
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

        /// <summary>
        /// Another writer may insert the same answer cell between the lookup and the insert.
        /// A second run finds that row and updates it instead.
        /// </summary>
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
                // The row was deleted or changed by another writer in between
                var entries = string.Join(", ", ex.Entries.Select(e => $"{e.Metadata.ClrType.Name} ({e.State})"));
                throw new InvalidOperationException($"Concurrency conflict on: {entries}", ex);
            }
        }

        private static Quiz? SortRounds(Quiz? quiz)
        {
            // Include gives no ordering guarantee (EF sorts by the random Guid key).
            // All consumers treat Rounds as chronological.
            if (quiz != null)
            {
                quiz.Rounds = [.. quiz.Rounds.OrderBy(r => r.CreatedAt)];
            }

            return quiz;
        }

        private async Task UpdateAnswersCoreAsync(Guid roundId,
            Dictionary<(Guid TeamId, int QuestionIndex), bool?> cellUpdates, CancellationToken ct)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quiz = await db.Quizzes
                .AsNoTracking()
                .Where(q => q.Rounds.Any(r => r.Id == roundId))
                .Select(q => new { q.Id, q.IsCompleted })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Round not found.");

            // Corrections on a completed night also have to update its materialized results
            await using var transaction = quiz.IsCompleted
                ? await db.Database.BeginTransactionAsync(ct)
                : null;

            var teamIds = cellUpdates.Keys
                .Select(k => k.TeamId)
                .Distinct()
                .ToArray();

            // Unique per cell, guaranteed by the database
            var lookup = await db.Answers
                .Where(a => a.RoundId == roundId && teamIds.Contains(a.TeamId))
                .ToDictionaryAsync(a => (a.TeamId, a.QuestionIndex), ct);

            foreach (var (key, value) in cellUpdates)
            {
                lookup.TryGetValue(key, out var existing);

                if (value == null)
                {
                    if (existing != null)
                    {
                        db.Answers.Remove(existing);
                    }

                    continue;
                }

                if (existing != null)
                {
                    existing.Value = new AnswerBool { Correct = value.Value };
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
                        Value = new AnswerBool { Correct = value.Value },
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

        /// <summary>
        /// Single place for all start-round rules, the dialog only shows the resulting message.
        /// </summary>
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

            var roundName = request.RoundName.Trim();
            if (roundName.Length == 0)
            {
                throw new InvalidOperationException("Round name is required.");
            }

            if (quiz.Rounds.Any(r => r.Name.Equals(roundName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Round \"{roundName}\" already exists.");
            }

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

            // Same count and same set means every participant appears exactly once
            var assignedTeamIds = request.Assignments.SelectMany(a => a.TeamIds).ToArray();
            var participantIds = quiz.ParticipatingTeams.Select(pt => pt.TeamId).ToHashSet();

            if (assignedTeamIds.Length != participantIds.Count || !participantIds.SetEquals(assignedTeamIds))
            {
                throw new InvalidOperationException(
                    $"All {participantIds.Count} teams must be assigned to exactly one scorer station.");
            }
        }

        /// <summary>
        /// Read-only graph for dashboard and export. Tracking it would only cost time on every reload.
        /// </summary>
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

        #endregion Private Methods
    }
}
