using PubQuizMaster.Core.Records.Player;
using PubQuizMaster.Services.Player;

namespace PubQuizMaster.Web.Pages.Player
{
    public partial class Standings
    {
        #region Private Fields

        private LeaderboardTeam[] entries = [];
        private bool isLoading = true;
        private string searchTerm = string.Empty;

        #endregion Private Fields

        #region Private Properties

        private LeaderboardTeam[] FilteredEntries => string.IsNullOrWhiteSpace(searchTerm)
            ? entries
            : [.. entries.Where(e => e.TeamName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))];

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            isLoading = true;
            entries = await LeaderboardService.GetLeaderboardAsync();
            isLoading = false;
        }

        #endregion Protected Methods
    }
}