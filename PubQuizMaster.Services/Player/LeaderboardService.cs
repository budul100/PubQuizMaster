using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Records.Player;
using PubQuizMaster.Core.Scoring;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Player
{
    public class LeaderboardService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<LeaderboardTeam[]> GetLeaderboardAsync(CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            // Legacy nights are imported as results, live nights are materialized on completion
            var totals = await db.Scores
                .AsNoTracking()
                .Where(s => s.Quiz.IsCompleted)
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

            // Ranked over all teams, so a filtered view still shows the real position
            return [.. CompetitionRanking.Rank(totals, x => x.Score)
                .Select(r => new LeaderboardTeam(r.Item.TeamId, r.Item.Name, r.Item.Score, r.Item.Count, r.Rank))];
        }

        #endregion Public Methods
    }
}
