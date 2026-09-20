using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Standings;
using PubQuizMaster.Services;

namespace PubQuizMaster.Web.Components.Player
{
    public partial class TeamSelector
        : ComponentBase, IDisposable
    {
        #region Private Fields

        private const int DebounceMilliseconds = 300;
        private const int MinSearchLength = 3;

        // Only ever holds the source of the search currently running, the owning handler disposes it
        private CancellationTokenSource? debounceCts;

        private ElementReference inputElement;
        private TeamMatch[] similarCandidates = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string AddButtonText { get; set; } = "Add";

        [Parameter] public EventCallback OnCreateNewConfirmed { get; set; }

        [Parameter] public EventCallback<Team> OnTeamSelected { get; set; }

        [Parameter] public string Placeholder { get; set; } = "Team name\u2026";

        [Parameter] public bool ShowAddButton { get; set; } = true;

        [Parameter] public string Value { get; set; } = string.Empty;

        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        #endregion Public Properties

        #region Private Properties

        [Inject] private ILogger<TeamSelector> Logger { get; set; } = null!;

        #endregion Private Properties

        #region Public Methods

        public void Dispose()
        {
            debounceCts?.Cancel();
        }

        public async Task FocusAsync()
        {
            try
            {
                await inputElement.FocusAsync();
            }
            catch
            {
                // Element detached
            }
        }

        #endregion Public Methods

        #region Private Methods

        private void CancelSearch()
        {
            debounceCts?.Cancel();
            similarCandidates = [];
        }

        private async Task ConfirmCreate()
        {
            CancelSearch();
            await OnCreateNewConfirmed.InvokeAsync();
        }

        // The EventCallback renders again when this handler completes, also after a cancellation
        private async Task HandleInputAsync(ChangeEventArgs e)
        {
            Value = e.Value?.ToString() ?? string.Empty;
            await ValueChanged.InvokeAsync(Value);

            // Previous suggestions stay visible until the new lookup is done, avoids flicker while typing
            debounceCts?.Cancel();

            var searchText = Value.Trim();
            if (searchText.Length < MinSearchLength)
            {
                similarCandidates = [];
                return;
            }

            using var cts = new CancellationTokenSource();
            debounceCts = cts;

            try
            {
                await Task.Delay(DebounceMilliseconds, cts.Token);

                var matches = await TeamMatchingService.FindSimilarTeamsAsync(
                    searchText, minThreshold: 0.65, maxResults: 4, ct: cts.Token);

                // A newer input may have cancelled this search without the query observing it
                if (!cts.IsCancellationRequested)
                {
                    similarCandidates = [.. matches];
                }
            }
            catch (OperationCanceledException)
            {
                // Superseded by newer input or component disposed
            }
            catch (Exception ex)
            {
                // Suggestions are optional, a failed lookup must not break the circuit
                Logger.LogWarning(ex, "Similar team lookup failed for '{SearchText}'.", searchText);
                similarCandidates = [];
            }
            finally
            {
                if (ReferenceEquals(debounceCts, cts))
                {
                    debounceCts = null;
                }
            }
        }

        private async Task SelectCandidate(Team team)
        {
            CancelSearch();
            Value = team.Name;
            await ValueChanged.InvokeAsync(Value);
            await OnTeamSelected.InvokeAsync(team);
        }

        #endregion Private Methods
    }
}