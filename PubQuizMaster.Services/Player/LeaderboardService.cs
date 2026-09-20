using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Records.Player;
using PubQuizMaster.Core.Scoring;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Player
{
    public class LeaderboardService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Private Fields

        /// <summary>
        /// Share of all quiz nights a team must have played to be ranked by average.
        /// Keeps a single lucky night from topping the average ranking.
        /// </summary>
        private const decimal AverageMinShare = 0.1m;

        #endregion Private Fields

        #region Public Methods

        public async Task<LeaderboardData> GetLeaderboardAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Legacy nights are imported as results, live nights are materialized on completion
            var completedResults = db.Scores
                .AsNoTracking()
                .Where(s => s.Quiz.IsCompleted);

            var totals = await completedResults
                .GroupBy(s => new { s.TeamId, s.Team.Name })
                .Select(g => new
                {
                    g.Key.TeamId,
                    g.Key.Name,
                    Score = g.Sum(s => s.TotalScore),
                    Count = g.Count()
                })
                .OrderBy(x => x.Name)
                .ToArrayAsync(ct);

            // Nights with at least one result, the same basis the counts per team come from
            var quizCount = await completedResults
                .Select(s => s.QuizId)
                .Distinct()
                .CountAsync(ct);

            // "At least 10 %" rounds up: 25 nights need 3, 30 nights need 3, 31 nights need 4
            var minQuizzes = Math.Max(1, (int)Math.Ceiling(quizCount * AverageMinShare));

            // Ranked over all teams, so a filtered view still shows the real position
            LeaderboardTeam[] teams = [.. CompetitionRanking.Rank(totals, x => x.Score)
                .Select(r => new LeaderboardTeam(r.Item.TeamId, r.Item.Name, r.Item.Score, r.Item.Count, r.Rank))];

            return new LeaderboardData(teams, quizCount, minQuizzes);
        }

        #endregion Public Methods
    }
}
