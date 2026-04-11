using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Services
{
    public static class RankingService
    {
        #region Public Methods

        public static decimal ComputeRoundAverage(IEnumerable<Team> masterTeamList, Round round)
        {
            var entries = GetRanksRound(
                masterTeamList: masterTeamList,
                round: round).ToArray();

            if (entries.Length == 0) return 0m;

            return entries.Average(e => e.Score);
        }

        public static decimal ComputeTotalAverage(IEnumerable<LeaderboardEntry> leaderboard)
        {
            if (!leaderboard.Any()) return 0m;

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

        public static IEnumerable<RankedEntry> GetRanksRound(IEnumerable<Team> masterTeamList, Round round)
        {
            var scores = round.ActiveTeamIds
                .Select(id => new
                {
                    Team = masterTeamList.FirstOrDefault(t => t.Id == id),
                    Score = round.GetTeamScore(id)
                })
                .Where(x => x.Team != null && x.Score.HasValue)
                .OrderByDescending(x => x.Score!.Value)
                .ThenBy(x => x.Team!.Name).ToList();

            var rank = 1;

            for (var index = 0; index < scores.Count; index++)
            {
                if (index > 0 && scores[index].Score != scores[index - 1].Score)
                {
                    rank = index + 1;
                }

                yield return new RankedEntry
                {
                    Rank = rank,
                    Team = scores[index].Team!,
                    Score = scores[index].Score!.Value
                };
            }
        }

        public static IEnumerable<RankedEntry> GetRanksTotal(IEnumerable<LeaderboardEntry> leaderboard)
        {
            var sorteds = leaderboard
                .Where(e => e.Team != null)
                .OrderByDescending(e => e.Rank)
                .ThenBy(e => e.Team.Name).ToArray();

            foreach (var sorted in sorteds)
            {
                yield return new RankedEntry
                {
                    Rank = sorted.Rank,
                    Team = sorted.Team,
                    Score = sorted.TotalScore
                };
            }
        }

        public static IEnumerable<Team> GetTeamsAtRank(IEnumerable<RankedEntry> ranking, int rank) => ranking
                          .Where(e => e.Rank == rank)
            .Select(e => e.Team).ToList();

        #endregion Public Methods
    }
}