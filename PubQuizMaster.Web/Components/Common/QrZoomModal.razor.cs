using Microsoft.AspNetCore.Components;

namespace PubQuizMaster.Web.Components.Common
{
    /// <summary>
    /// Full-screen QR code of a scorer station, so scorers can scan it from their seat.
    /// </summary>
    public partial class QrZoomModal
    {
        #region Public Properties

        [Parameter] public bool IsOpen { get; set; }

        [Parameter] public string Label { get; set; } = string.Empty;

        [Parameter] public EventCallback OnClosed { get; set; }

        [Parameter] public string QrCodeDataUrl { get; set; } = string.Empty;

        [Parameter] public string Url { get; set; } = string.Empty;

        #endregion Public Properties

        #region Private Methods

        private Task Close() => OnClosed.InvokeAsync();

        #endregion Private Methods
    }
}
