using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;

namespace PubQuizMaster.Web.Pages.Standings
{
    /// <summary>
    /// Team registry: register, rename inline in the list, merge duplicates and see a team's quiz nights.
    /// </summary>
    public partial class Teams
    {
        #region Private Fields

        private string filterQuery = string.Empty;
        private int historyVersion;
        private bool isBusy;
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

        private string MergeTargetName => teams.FirstOrDefault(t => t.Id == targetMergeTeamId)?.Name
            ?? string.Empty;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            await LoadTeamsAsync();

            if (TeamId is { } teamId)
            {
                SelectTeamById(teamId);
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
                SelectTeamById(team.Id);
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
                SelectTeamById(target.Id);
            }
            else
            {
                selectedTeam = null;
            }

            historyVersion++;
        }

        private async Task HandleRenamedAsync(Team team)
        {
            await LoadTeamsAsync();
            SelectTeamById(team.Id);
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
                SelectTeamById(targetId);

                historyVersion++;
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

        private void SelectExistingTeam(Team team)
        {
            newTeamName = string.Empty;
            SelectTeam(team);
        }

        private void SelectTeam(Team team)
        {
            // Clicks into the inline editor of the selected row bubble up here, keep the merge target then
            if (selectedTeam?.Id == team.Id) return;

            selectedTeam = team;
            targetMergeTeamId = Guid.Empty;
        }

        private void SelectTeamById(Guid teamId)
        {
            selectedTeam = teams.FirstOrDefault(t => t.Id == teamId);
            targetMergeTeamId = Guid.Empty;
        }

        #endregion Private Methods
    }
}
