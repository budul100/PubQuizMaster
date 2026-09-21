namespace PubQuizMaster.Web.Components.Common
{
    public partial class ToastVisualizer
    {
        #region Private Fields

        private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(Constants.ToastVisualizerSeconds);

        private CancellationTokenSource? cts;
        private bool isError;
        private string message = string.Empty;
        private bool visible;

        #endregion Private Fields

        #region Public Methods

        public void Dispose()
        {
            ToastService.OnShow -= Show;
            CancelPending();

            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected override void OnInitialized() => ToastService.OnShow += Show;

        #endregion Protected Methods

        #region Private Methods

        private async Task AutoHideAsync(CancellationToken token)
        {
            try
            {
                await Task.Delay(DisplayDuration, token);
                await InvokeAsync(Hide);
            }
            catch (OperationCanceledException)
            {
                // Replaced by a newer toast or component disposed
            }
            catch (ObjectDisposedException)
            {
                // Circuit already gone
            }
        }

        private void CancelPending()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private void Hide()
        {
            visible = false;
            StateHasChanged();
        }

        private void Show(string msg, bool error)
        {
            _ = InvokeAsync(() =>
            {
                CancelPending();
                cts = new CancellationTokenSource();

                message = msg;
                isError = error;
                visible = true;
                StateHasChanged();

                _ = AutoHideAsync(cts.Token);
            });
        }

        #endregion Private Methods
    }
}