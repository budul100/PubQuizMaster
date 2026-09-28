using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Standings;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Standings
{
    /// <summary>
    /// Completed quiz nights of one team: points against the night average as a chart, plus a table.
    /// Loads its data itself whenever the team changes. Recreate it with a @key to force a reload.
    /// </summary>
    public partial class TeamHistoryPanel
    {
        #region Private Fields

        private TeamHistoryEntry[] history = [];
        private bool isLoading = true;
        private Guid loadedTeamId;

        #endregion Private Fields

        #region Public Properties

        /// <summary>Adds a link to the team registry, for pages other than the registry itself.</summary>
        [Parameter] public bool ShowTeamsLink { get; set; }

        [Parameter, EditorRequired] public Guid TeamId { get; set; }

        [Parameter, EditorRequired] public string TeamName { get; set; } = string.Empty;

        #endregion Public Properties

        #region Private Properties

        private string[] Labels => [.. history.Select(h => h.Date.ToString("dd.MM.yy"))];

        /// <summary>The team's points against the average of the same night.</summary>
        private ChartSeries[] Series =>
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

        [Inject] private TeamService TeamService { get; set; } = null!;

        [Inject] private ToastService ToastService { get; set; } = null!;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnParametersSetAsync()
        {
            if (TeamId == loadedTeamId) return;

            var teamId = TeamId;
            loadedTeamId = teamId;
            isLoading = true;
            history = [];

            try
            {
                var loaded = await TeamService.GetHistoryAsync(teamId);

                // Another team may have been selected meanwhile
                if (TeamId == teamId)
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
                isLoading = false;
            }
        }

        #endregion Protected Methods
    }
}
