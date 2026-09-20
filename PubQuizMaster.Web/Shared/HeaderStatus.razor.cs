using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Event;

namespace PubQuizMaster.Web.Shared
{
    /// <summary>
    /// Status badges in the top row: name of the running round and number of scorer stations online.
    /// Renders nothing while no round of the active quiz night is open.
    /// </summary>
    public partial class HeaderStatus : IDisposable
    {
        #region Private Fields

        // Going offline raises no event (ScorerService only knows the last heartbeat, window 25 s),
        // so the online count is re-evaluated periodically. No database access.
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

        private readonly CancellationTokenSource cts = new();
        private ActiveRound? info;
        private PeriodicTimer? refreshTimer;

        #endregion Private Fields

        #region Private Properties

        [Inject] private QuizService LiveQuizService { get; set; } = null!;

        [Inject] private ILogger<HeaderStatus> Logger { get; set; } = null!;

        private int OnlineCount => info?.ScorerIds.Count(SessionService.IsConnected) ?? 0;

        [Inject] private ScorerService SessionService { get; set; } = null!;

        #endregion Private Properties

        #region Public Methods

        public void Dispose()
        {
            SessionService.OnRoundChanged -= HandleRoundChanged;
            SessionService.OnStatusChanged -= HandleStatusChanged;
            cts.Cancel();
            cts.Dispose();
            refreshTimer?.Dispose();
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();

            if (!RendererInfo.IsInteractive) return;

            SessionService.OnRoundChanged += HandleRoundChanged;
            SessionService.OnStatusChanged += HandleStatusChanged;

            refreshTimer = new PeriodicTimer(RefreshInterval);
            _ = RefreshLoopAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private void HandleRoundChanged()
        {
            _ = InvokeAsync(async () =>
            {
                await LoadAsync();
                StateHasChanged();
            });
        }

        private void HandleStatusChanged()
        {
            // Heartbeats arrive every few seconds per scorer, skip them while nothing is shown
            if (info == null) return;

            _ = InvokeAsync(StateHasChanged);
        }

        private async Task LoadAsync()
        {
            try
            {
                info = await LiveQuizService.GetActiveRoundInfoAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Loading the header status failed.");
            }
        }

        private async Task RefreshLoopAsync()
        {
            var token = cts.Token;

            try
            {
                while (refreshTimer != null && await refreshTimer.WaitForNextTickAsync(token))
                {
                    if (info != null)
                    {
                        await InvokeAsync(StateHasChanged);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        #endregion Private Methods
    }
}
