using Microsoft.AspNetCore.Components;
using PubQuizMaster.Web.Components.Common;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Standings
{
    public partial class TeamPanel
    {
        #region Private Fields

        private string newTeamName = string.Empty;
        private bool showAddTeamForm;
        private TeamSelector? teamSelectorRef;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public EventCallback<string> OnRegisterTeam { get; set; }

        [Parameter] public EventCallback<Guid> OnRemoveTeam { get; set; }

        [Parameter] public EventCallback<(Guid TeamId, bool IsActive, bool IsNonCompetitive)> OnUpdateStatus { get; set; }

        [Parameter] public TeamStanding[] Standings { get; set; } = [];

        #endregion Public Properties

        #region Private Methods

        private async Task HandleExistingTeamSelected(string teamName)
        {
            newTeamName = string.Empty;
            await OnRegisterTeam.InvokeAsync(teamName);
            if (teamSelectorRef != null) await teamSelectorRef.FocusAsync();
        }

        private async Task HandleNewTeamCreated()
        {
            if (string.IsNullOrWhiteSpace(newTeamName)) return;
            var name = newTeamName.Trim();
            newTeamName = string.Empty;
            await OnRegisterTeam.InvokeAsync(name);
            if (teamSelectorRef != null) await teamSelectorRef.FocusAsync();
        }

        private async Task ToggleActiveAsync(Guid teamId, bool newActive)
        {
            var current = Standings.FirstOrDefault(s => s.TeamId == teamId);
            if (current != null)
            {
                await OnUpdateStatus.InvokeAsync((teamId, newActive, current.IsNonCompetitive));
            }
        }

        private async Task ToggleNonCompetitiveAsync(Guid teamId, bool isNonCompetitive)
        {
            var current = Standings.FirstOrDefault(s => s.TeamId == teamId);
            if (current != null)
            {
                await OnUpdateStatus.InvokeAsync((teamId, current.IsActive, isNonCompetitive));
            }
        }

        #endregion Private Methods
    }
}