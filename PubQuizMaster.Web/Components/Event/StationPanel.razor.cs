using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Event;
using PubQuizMaster.Web.Helpers;

namespace PubQuizMaster.Web.Components.Event
{
    /// <summary>
    /// Scorer stations of a quiz night with QR code, token and online status.
    /// Shown before the first round and between rounds; while a round is open,
    /// the scoring banner takes over.
    /// </summary>
    public partial class StationPanel
    {
        #region Private Fields

        private bool isBusy;
        private ScorerStation? zoomStation;

        #endregion Private Fields

        #region Public Properties

        /// <summary>Raised after a station was added, changed or removed. The page reloads.</summary>
        [Parameter] public EventCallback OnChanged { get; set; }

        [Parameter, EditorRequired] public Guid QuizId { get; set; }

        /// <summary>Stations of the quiz night in label order.</summary>
        [Parameter] public ScorerStation[] Stations { get; set; } = [];

        #endregion Public Properties

        #region Private Properties

        [Inject] private NavigationManager Nav { get; set; } = null!;

        [Inject] private QrCodeService QrCodeService { get; set; } = null!;

        [Inject] private ScorerService SessionService { get; set; } = null!;

        [Inject] private StationService StationService { get; set; } = null!;

        [Inject] private ToastService ToastService { get; set; } = null!;

        #endregion Private Properties

        #region Private Methods

        private Task AddStationAsync() => RunAsync(() => StationService.AddStationAsync(
            quizId: QuizId,
            label: StationLabels.Next(Stations.Select(s => s.Label)),
            scorerId: ScorerTokens.Create()));

        private string GetScorerUrl(string scorerId)
        {
            var baseUri = Nav.BaseUri.TrimEnd('/');
            return $"{baseUri}/scorer/{Uri.EscapeDataString(scorerId)}";
        }

        private Task RegenerateTokenAsync(ScorerStation station) => RunAsync(() => StationService.UpdateStationAsync(
            stationId: station.Id,
            label: station.Label,
            scorerId: ScorerTokens.Create()));

        private Task RemoveStationAsync(ScorerStation station) => RunAsync(
            () => StationService.RemoveStationAsync(station.Id));

        private async Task RenameStationAsync(ScorerStation station, string? label)
        {
            if (string.IsNullOrWhiteSpace(label) || label.Trim() == station.Label) return;

            await RunAsync(() => StationService.UpdateStationAsync(
                stationId: station.Id,
                label: label,
                scorerId: station.ScorerId));
        }

        private async Task RunAsync(Func<Task> action)
        {
            if (isBusy) return;

            isBusy = true;

            try
            {
                await action();
                await OnChanged.InvokeAsync();
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

        #endregion Private Methods
    }
}
