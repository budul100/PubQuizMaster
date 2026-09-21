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

        private static LeaderboardRow[] RankBy(IEnumerable<LeaderboardTeam> teams,
            Func<LeaderboardTeam, decimal> metric)
        {
            return [.. teams.Rank(metric).Select(r => new LeaderboardRow(r.Item, r.Rank))];
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

            rankedRows = sort switch
            {
                // Ranked on the value as displayed (one decimal), so equal-looking averages share a rank
                StandingsSort.Average => RankBy(
                    teams: teams.Where(t => t.QuizzesPlayed >= data.MinQuizzesForAverage),
                    metric: t => Math.Round(t.AverageScore, 1)),

                StandingsSort.Quizzes => RankBy(
                    teams: teams,
                    metric: t => t.QuizzesPlayed),

                _ => RankBy(
                    teams: teams,
                    metric: t => t.TotalScore)
            };
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