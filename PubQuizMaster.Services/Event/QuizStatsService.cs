using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Difficulty statistics across quiz nights: how many questions were answered correctly.
    /// Imported nights take part when their question count is known.
    /// </summary>
    public class QuizStatsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        /// <summary>
        /// Correct share of every played night in date order. Live nights count their recorded rounds
        /// (answer values are jsonb, so the rounds are loaded and counted in memory). Imported nights
        /// need their question count: average points per team divided by the questions.
        /// </summary>
        public async Task<QuizDifficulty[]> GetDifficultiesAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var liveQuizzes = await db.Quizzes
                .AsNoTracking()
                .Where(q => !q.IsLegacyImport && q.Status != QuizStatus.Planned && q.Rounds.Any())
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Assignments)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Answers)
                .AsSplitQuery()
                .ToArrayAsync(ct);

            var liveDifficulties = liveQuizzes
                .Select(q => (Quiz: q, Rate: q.Rounds.GetCorrectRate()))
                .Where(x => x.Rate.HasValue)
                .Select(x => new QuizDifficulty(
                    QuizId: x.Quiz.Id,
                    Date: x.Quiz.Date,
                    Title: x.Quiz.Title,
                    Status: x.Quiz.Status,
                    CorrectRate: x.Rate!.Value));

            var importedDifficulties = await db.Quizzes
                .AsNoTracking()
                .Where(q => q.IsLegacyImport && q.ImportedQuestionCount > 0 && q.Results.Any())
                .Select(q => new QuizDifficulty(
                    q.Id,
                    q.Date,
                    q.Title,
                    q.Status,
                    q.Results.Average(r => r.TotalScore) / q.ImportedQuestionCount!.Value))
                .ToArrayAsync(ct);

            return [.. liveDifficulties
                .Concat(importedDifficulties)
                .OrderBy(d => d.Date)];
        }

        #endregion Public Methods
    }
}
