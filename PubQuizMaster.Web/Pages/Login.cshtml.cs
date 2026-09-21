using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace PubQuizMaster.Web.Pages
{
    [EnableRateLimiting(Constants.LoginPolicyName)]
    public class LoginModel(IConfiguration configuration, ILogger<LoginModel> logger)
        : PageModel
    {
        #region Public Properties

        public string? ErrorMessage { get; private set; }

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        #endregion Public Properties

        #region Public Methods

        public IActionResult OnGet()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return LocalRedirect(GetSafeReturnUrl());
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var expected = configuration[Constants.LoginPasswordKey];

            if (string.IsNullOrEmpty(expected) || !PasswordMatches(Password, expected))
            {
                logger.LogWarning("Failed admin login from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
                ErrorMessage = "Invalid password.";
                return Page();
            }

            var identity = new ClaimsIdentity(
                claims: [new Claim(ClaimTypes.Name, "admin")],
                authenticationType: CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                scheme: CookieAuthenticationDefaults.AuthenticationScheme,
                principal: new ClaimsPrincipal(identity),
                properties: new AuthenticationProperties { IsPersistent = true });

            return LocalRedirect(GetSafeReturnUrl());
        }

        #endregion Public Methods

        #region Private Methods

        private static bool PasswordMatches(string input, string expected)
        {
            // Hashing first gives equal lengths, so the comparison runs in constant time
            var inputHash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
            return CryptographicOperations.FixedTimeEquals(inputHash, expectedHash);
        }

        private string GetSafeReturnUrl() => !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? ReturnUrl
            : "/";

        #endregion Private Methods
    }
}