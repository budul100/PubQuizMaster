using Microsoft.AspNetCore.Components;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Player
{
    public partial class TeamListPanel
    {
        #region Private Fields

        private string newTeamName = string.Empty;
        private bool showAddTeamForm;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public EventCallback<string> OnRegisterTeam { get; set; }

        [Parameter] public TeamStanding[] Standings { get; set; } = [];

        #endregion Public Properties

        #region Private Methods

        private async Task HandleExistingTeamSelected(string teamName)
        {
            newTeamName = string.Empty;
            showAddTeamForm = false;
            await OnRegisterTeam.InvokeAsync(teamName);
        }

        private async Task HandleNewTeamCreated()
        {
            if (string.IsNullOrWhiteSpace(newTeamName)) return;
            var name = newTeamName.Trim();
            newTeamName = string.Empty;
            showAddTeamForm = false;
            await OnRegisterTeam.InvokeAsync(name);
        }

        #endregion Private Methods
    }
}