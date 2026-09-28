using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Difficulty statistics across quiz nights: how many questions were answered correctly.
    /// Legacy imports only carry totals and are left out.
    /// </summary>
    public class QuizStatsService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        /// <summary>
        /// Correct share of every live or completed night with recorded rounds, in date order.
        /// Answer values are jsonb, so the rounds are loaded and counted in memory.
        /// </summary>
        public async Task<QuizDifficulty[]> GetDifficultiesAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var quizzes = await db.Quizzes
                .AsNoTracking()
                .Where(q => !q.IsLegacyImport && q.Status != QuizStatus.Planned && q.Rounds.Any())
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Assignments)
                .Include(q => q.Rounds)
                    .ThenInclude(r => r.Answers)
                .AsSplitQuery()
                .ToArrayAsync(ct);

            return [.. quizzes
                .Select(q => (Quiz: q, Rate: q.Rounds.GetCorrectRate()))
                .Where(x => x.Rate.HasValue)
                .OrderBy(x => x.Quiz.Date)
                .Select(x => new QuizDifficulty(
                    QuizId: x.Quiz.Id,
                    Date: x.Quiz.Date,
                    Title: x.Quiz.Title,
                    Status: x.Quiz.Status,
                    CorrectRate: x.Rate!.Value))];
        }

        #endregion Public Methods
    }
}
