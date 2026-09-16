using Microsoft.AspNetCore.Components;

namespace PubQuizMaster.Web.Components.Common
{
    public partial class ConfirmModal
    {
        #region Public Properties

        [Parameter] public string CancelText { get; set; } = "Cancel";

        [Parameter] public string ConfirmButtonClass { get; set; } = "btn-danger";

        [Parameter] public string ConfirmText { get; set; } = "Delete";

        [Parameter] public string HeaderClass { get; set; } = "bg-danger";

        [Parameter] public bool IsOpen { get; set; }

        [Parameter] public string Message { get; set; } = "Are you sure you want to proceed?";

        [Parameter] public EventCallback OnCanceled { get; set; }

        [Parameter] public EventCallback OnConfirmed { get; set; }

        [Parameter] public string Title { get; set; } = "Confirm Action";

        #endregion Public Properties

        #region Private Methods

        private async Task Cancel() => await OnCanceled.InvokeAsync();

        private async Task Confirm() => await OnConfirmed.InvokeAsync();

        #endregion Private Methods
    }
}