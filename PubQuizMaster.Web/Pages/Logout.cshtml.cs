using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PubQuizMaster.Web.Pages
{
    public class LogoutModel
        : PageModel
    {
        #region Public Methods

        // GET only asks for confirmation, a plain link or image on another site must not sign out
        public IActionResult OnGet()
        {
            return User.Identity?.IsAuthenticated == true
                ? Page()
                : LocalRedirect("/login");
        }

        // Razor Pages validate the antiforgery token of the form automatically
        public async Task<IActionResult> OnPostAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return LocalRedirect("/login");
        }

        #endregion Public Methods
    }
}
