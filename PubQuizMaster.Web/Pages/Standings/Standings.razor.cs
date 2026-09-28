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

        /// <summary>
        /// Average as displayed (one decimal), so equal-looking averages share a rank.
        /// Used for ranking and ordering alike.
        /// </summary>
        private static decimal AverageMetric(LeaderboardTeam team) => Math.Round(team.AverageScore, 1);

        /// <summary>
        /// Same rule as within a quiz night: teams out of competition get the rank of the next
        /// regular team below them and do not push the regular teams down.
        /// </summary>
        private static Dictionary<Guid, int> RankLookup(IEnumerable<LeaderboardTeam> teams,
            Func<LeaderboardTeam, decimal> metric)
        {
            return teams
                .Rank(
                    score: metric,
                    isNonCompetitive: t => t.IsNonCompetitive)
                .ToDictionary(r => r.Item.TeamId, r => r.Rank);
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
                    comparer: TeamNameComparer.Instance).ToArray();

            var rankedByAverage = teams
                .Where(t => t.QuizzesPlayed >= data.MinQuizzesForAverage).ToArray();

            var quizzesRanks = RankLookup(
                teams: teams,
                metric: t => t.QuizzesPlayed);

            var totalRanks = RankLookup(
                teams: teams,
                metric: t => t.TotalScore);

            var averageRanks = RankLookup(
                teams: rankedByAverage,
                metric: AverageMetric);

            // Ordered by the value, not by the rank: a team out of competition shares the rank of the
            // next regular team below it, but is listed at the position its value earns.
            // On equal values the regular team comes first, as in the quiz night ranking.
            // OrderBy is stable, so the alphabetical input order survives after that.
            IEnumerable<LeaderboardTeam> ordered = sort switch
            {
                StandingsSort.Average => rankedByAverage
                    .OrderByDescending(AverageMetric)
                    .ThenBy(t => t.IsNonCompetitive),

                StandingsSort.Quizzes => teams
                    .OrderByDescending(t => t.QuizzesPlayed)
                    .ThenBy(t => t.IsNonCompetitive),

                _ => teams
                    .OrderByDescending(t => t.TotalScore)
                    .ThenBy(t => t.IsNonCompetitive)
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

        private async Task SetNonCompetitiveAsync(Guid teamId, bool isNonCompetitive)
        {
            if (data == null) return;

            try
            {
                await TeamService.SetNonCompetitiveAsync(teamId, isNonCompetitive);

                // Local update instead of a reload: the totals are unchanged, only the ranks move
                data = data with
                {
                    Teams = [.. data.Teams.Select(t => t.TeamId == teamId
                        ? t with { IsNonCompetitive = isNonCompetitive }
                        : t)]
                };

                ApplySort();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private void SetSort(StandingsSort newSort)
        {
            if (sort == newSort) return;

            sort = newSort;
            ApplySort();
        }

        #endregion Private Methods
    }
}