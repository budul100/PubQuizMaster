using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Services
{
    public static class RankingService
    {
        #region Public Methods

        public static decimal ComputeRoundAverage(Round round, List<Team> masterTeamList)
        {
            var entries = RankRound(round, masterTeamList);
            if (entries.Count == 0) return 0m;
            return entries.Average(e => e.Score);
        }

        public static decimal ComputeTotalAverage(List<LeaderboardEntry> leaderboard)
        {
            if (leaderboard.Count == 0) return 0m;
            return leaderboard.Average(e => e.TotalScore);
        }

        public static string FormatAverage(decimal average)
        {
            var rounded = Math.Round(average, 1);
            var formatted = rounded.ToString("0.#", System.Globalization.CultureInfo.GetCultureInfo("de-DE"));

            return $"{formatted} Punkte / Team";
        }

        public static string FormatScore(decimal score)
        {
            var rounded = Math.Round(score, 1);
            return rounded == 1m ? "1 Punkt" : $"{rounded:0.#} Punkte";
        }

        public static List<Team> GetTeamsAtRank(List<RankedEntry> ranking, int rank) => ranking
            .Where(e => e.Rank == rank)
            .Select(e => e.Team).ToList();

        public static List<RankedEntry> RankRound(Round round, List<Team> masterTeamList)
        {
            var scores = round.ActiveTeamIds
                .Select(id => new
                {
                    Team = masterTeamList.FirstOrDefault(t => t.Id == id),
                    Score = round.GetTeamScore(id)
                })
                .Where(x => x.Team != null && x.Score.HasValue)
                .OrderByDescending(x => x.Score!.Value)
                .ThenBy(x => x.Team!.Name)
                .ToList();

            var result = new List<RankedEntry>();
            int rank = 1;

            for (int i = 0; i < scores.Count; i++)
            {
                if (i > 0 && scores[i].Score != scores[i - 1].Score)
                    rank = i + 1;

                result.Add(new RankedEntry
                {
                    Rank = rank,
                    Team = scores[i].Team!,
                    Score = scores[i].Score!.Value
                });
            }

            return result;
        }

        public static List<RankedEntry> RankTotal(List<LeaderboardEntry> leaderboard)
        {
            var sorted = leaderboard
                .Where(e => e.Team != null)
                .OrderByDescending(e => e.TotalScore)
                .ThenBy(e => e.Team.Name)
                .ToList();

            var result = new List<RankedEntry>();
            int rank = 1;

            for (int i = 0; i < sorted.Count; i++)
            {
                if (i > 0 && sorted[i].TotalScore != sorted[i - 1].TotalScore)
                    rank = i + 1;

                result.Add(new RankedEntry
                {
                    Rank = rank,
                    Team = sorted[i].Team,
                    Score = sorted[i].TotalScore
                });
            }

            return result;
        }

        #endregion Public Methods
    }
}