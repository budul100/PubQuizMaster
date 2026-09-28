using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace PubQuizMaster.Web.Components.Common
{
    /// <summary>
    /// Search and filter field with a clear button. Filters while typing, Escape clears.
    /// Use with @bind-Value.
    /// </summary>
    public partial class SearchBox
    {
        #region Public Properties

        [Parameter] public string? AriaLabel { get; set; }

        [Parameter] public string Placeholder { get; set; } = "Search…";

        /// <summary>Inline style of the group, e.g. a fixed width in a card header.</summary>
        [Parameter] public string? Style { get; set; }

        [Parameter] public string Value { get; set; } = string.Empty;

        [Parameter] public EventCallback<string> ValueChanged { get; set; }

        #endregion Public Properties

        #region Private Methods

        private Task ClearAsync() => SetValueAsync(string.Empty);

        private Task HandleInputAsync(ChangeEventArgs e) => SetValueAsync(e.Value?.ToString() ?? string.Empty);

        private Task HandleKeyDownAsync(KeyboardEventArgs e) => e.Key == "Escape"
            ? ClearAsync()
            : Task.CompletedTask;

        private async Task SetValueAsync(string value)
        {
            Value = value;
            await ValueChanged.InvokeAsync(value);
        }

        #endregion Private Methods
    }
}
