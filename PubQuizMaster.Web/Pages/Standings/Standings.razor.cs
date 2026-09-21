using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Web.Enums;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Pages.Standings
{
    public partial class Standings
    {
        #region Private Fields

        private LeaderboardData? data;
        private bool isLoading = true;
        private LeaderboardRow[] rankedRows = [];
        private string searchTerm = string.Empty;
        private StandingsSort sort = StandingsSort.Total;

        #endregion Private Fields

        #region Private Properties

        /// <summary>Search filters after ranking, so a filtered view still shows the real position.</summary>
        private LeaderboardRow[] FilteredRows => !string.IsNullOrWhiteSpace(searchTerm)
            ? [.. rankedRows.Where(r => r.Team.TeamName.Contains(
                value: searchTerm,
                comparisonType: StringComparison.OrdinalIgnoreCase))]
            : rankedRows;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            isLoading = true;
            data = await LeaderboardService.GetLeaderboardAsync();

            ApplySort();
            isLoading = false;
        }

        #endregion Protected Methods

        #region Private Methods

        private static Dictionary<Guid, int> RankLookup(IEnumerable<LeaderboardTeam> teams,
            Func<LeaderboardTeam, decimal> metric)
        {
            return teams.Rank(metric).ToDictionary(r => r.Item.TeamId, r => r.Rank);
        }

        private void ApplySort()
        {
            if (data == null)
            {
                rankedRows = [];
                return;
            }

            // Tie order within a rank: team name, same comparer as the other lists
            var teams = data.Teams
                .OrderBy(
                    keySelector: t => t.TeamName,
                    comparer: StringComparer.CurrentCultureIgnoreCase).ToArray();

            var rankedByAverage = teams
                .Where(t => t.QuizzesPlayed >= data.MinQuizzesForAverage).ToArray();

            var quizzesRanks = RankLookup(
                teams: teams,
                metric: t => t.QuizzesPlayed);

            var totalRanks = RankLookup(
                teams: teams,
                metric: t => t.TotalScore);

            // Ranked on the value as displayed (one decimal), so equal-looking averages share a rank
            var averageRanks = RankLookup(
                teams: rankedByAverage,
                metric: t => Math.Round(t.AverageScore, 1));

            // OrderBy is stable, so the alphabetical input order survives inside a shared rank
            IEnumerable<LeaderboardTeam> ordered = sort switch
            {
                StandingsSort.Average => rankedByAverage.OrderBy(t => averageRanks[t.TeamId]),

                StandingsSort.Quizzes => teams.OrderBy(t => quizzesRanks[t.TeamId]),

                _ => teams.OrderBy(t => totalRanks[t.TeamId])
            };

            rankedRows = [.. ordered.Select(t => new LeaderboardRow(
                Team: t,
                QuizzesRank: quizzesRanks[t.TeamId],
                TotalRank: totalRanks[t.TeamId],
                AverageRank: averageRanks.GetValueOrDefault(t.TeamId)))];
        }

        /// <summary>Highlights the cells of the column the ranking is based on.</summary>
        private string CellClass(StandingsSort column) => sort == column
            ? "fw-bold text-primary"
            : "text-muted";

        private string HeaderClass(StandingsSort column) => sort == column
            ? "text-body fw-bold"
            : string.Empty;

        private void SetSort(StandingsSort newSort)
        {
            if (sort == newSort) return;

            sort = newSort;
            ApplySort();
        }

        #endregion Private Methods
    }
}