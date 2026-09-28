using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Pages.Standings
{
    /// <summary>
    /// Team registry: register, rename inline in the list, see a team's quiz nights and merge duplicates.
    /// </summary>
    public partial class Teams
    {
        #region Private Fields

        private string filterQuery = string.Empty;
        private TeamHistoryEntry[] history = [];
        private bool isBusy;
        private bool isLoadingHistory;
        private string newTeamName = string.Empty;
        private Team? selectedTeam;
        private bool showMergeModal;
        private Guid targetMergeTeamId = Guid.Empty;
        private Team[] teams = [];

        #endregion Private Fields

        #region Public Properties

        /// <summary>Preselects a team, e.g. when coming from the standings (/teams?teamId=...).</summary>
        [SupplyParameterFromQuery]
        public Guid? TeamId { get; set; }

        #endregion Public Properties

        #region Private Properties

        private IEnumerable<Team> FilteredTeams => !string.IsNullOrWhiteSpace(filterQuery)
            ? teams.Where(t => t.Name.Contains(
                value: filterQuery,
                comparisonType: StringComparison.OrdinalIgnoreCase))
            : teams;

        private string[] HistoryLabels => [.. history.Select(h => h.Date.ToString("dd.MM.yy"))];

        /// <summary>The team's points against the average of the same night.</summary>
        private ChartSeries[] HistorySeries =>
        [
            new ChartSeries(
                Name: "Points",
                Values: [.. history.Select(h => (decimal?)h.Score)],
                Color: "var(--bs-primary)",
                Tooltips: [.. history.Select(h => $"{h.Title}: {h.Score:0.#} points"
                    + (h.Rank is { } rank ? $", place {rank} of {h.TeamCount}" : string.Empty))]),
            new ChartSeries(
                Name: "Night average",
                Values: [.. history.Select(h => (decimal?)h.AverageScore)],
                Color: "var(--bs-secondary)")
        ];

        private string MergeTargetName => teams.FirstOrDefault(t => t.Id == targetMergeTeamId)?.Name
            ?? string.Empty;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            await LoadTeamsAsync();

            if (TeamId is { } teamId)
            {
                await SelectTeamByIdAsync(teamId);
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private static string FormatMergeResult(TeamMerge result)
        {
            var message = $"Merged '{result.SourceName}' into '{result.TargetName}'.";

            if (result.DroppedAnswers > 0 || result.DroppedResults > 0)
            {
                message += $" Kept the target's entries for {result.DroppedAnswers} conflicting answer(s) " +
                    $"and {result.DroppedResults} legacy result(s).";
            }

            return message;
        }

        private async Task CreateTeamAsync()
        {
            if (string.IsNullOrWhiteSpace(newTeamName) || isBusy) return;

            isBusy = true;

            try
            {
                var team = await TeamService.CreateTeamAsync(newTeamName);

                newTeamName = string.Empty;
                ToastService.ShowSuccess($"Team '{team.Name}' registered.");

                await LoadTeamsAsync();
                await SelectTeamByIdAsync(team.Id);
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isBusy = false;
            }
        }

        private async Task HandleMergedAsync(TeamMerge result)
        {
            // The merged team no longer exists, continue with the team it was merged into
            var target = teams.FirstOrDefault(t => t.Name == result.TargetName);

            await LoadTeamsAsync();

            if (target != null)
            {
                await SelectTeamByIdAsync(target.Id);
            }
            else
            {
                selectedTeam = null;
            }
        }

        private async Task HandleRenamedAsync(Team team)
        {
            await LoadTeamsAsync();
            await SelectTeamByIdAsync(team.Id);
        }

        private async Task LoadHistoryAsync()
        {
            if (selectedTeam == null)
            {
                history = [];
                return;
            }

            var teamId = selectedTeam.Id;
            isLoadingHistory = true;

            try
            {
                var loaded = await TeamService.GetHistoryAsync(teamId);

                // Another team may have been selected meanwhile
                if (selectedTeam?.Id == teamId)
                {
                    history = loaded;
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isLoadingHistory = false;
            }
        }

        private async Task LoadTeamsAsync()
        {
            teams = await TeamService.GetTeamsAsync();
        }

        private async Task MergeTeamsAsync()
        {
            showMergeModal = false;
            if (selectedTeam == null || targetMergeTeamId == Guid.Empty || isBusy) return;

            isBusy = true;

            try
            {
                var result = await TeamService.MergeTeamsAsync(
                    sourceTeamId: selectedTeam.Id,
                    targetTeamId: targetMergeTeamId);

                ToastService.ShowSuccess(FormatMergeResult(result));

                var targetId = targetMergeTeamId;
                await LoadTeamsAsync();
                await SelectTeamByIdAsync(targetId);
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Merge failed: {ex.Message}");
            }
            finally
            {
                isBusy = false;
            }
        }

        private async Task SelectExistingTeamAsync(Team team)
        {
            newTeamName = string.Empty;
            await SelectTeamAsync(team);
        }

        private async Task SelectTeamAsync(Team team)
        {
            // Clicks into the inline editor of the selected row bubble up here, nothing to reload then
            if (selectedTeam?.Id == team.Id) return;

            selectedTeam = team;
            targetMergeTeamId = Guid.Empty;
            history = [];

            await LoadHistoryAsync();
        }

        private async Task SelectTeamByIdAsync(Guid teamId)
        {
            var team = teams.FirstOrDefault(t => t.Id == teamId);

            if (team == null)
            {
                selectedTeam = null;
                history = [];
                return;
            }

            // Reload even if already selected: the name or the merged history may have changed
            selectedTeam = team;
            targetMergeTeamId = Guid.Empty;

            await LoadHistoryAsync();
        }

        #endregion Private Methods
    }
}
