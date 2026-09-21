using Microsoft.AspNetCore.Components;

namespace PubQuizMaster.Web.Components.Common
{
    public partial class LoginRedirect
    {
        #region Private Properties

        [Inject] private NavigationManager Nav { get; set; } = null!;

        #endregion Private Properties

        #region Protected Methods

        protected override void OnInitialized()
        {
            var returnUrl = "/" + Nav.ToBaseRelativePath(Nav.Uri);

            // Full page load: login is a Razor Page, not a Blazor route
            Nav.NavigateTo($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}", forceLoad: true);
        }

        #endregion Protected Methods
    }
}