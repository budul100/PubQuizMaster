namespace PubQuizMaster.Web.Security
{
    /// <summary>
    /// Rate limit settings for the login page (brute-force protection).
    /// </summary>
    public static class LoginRateLimit
    {
        #region Public Fields

        public const int PermitLimit = 10;

        public const string PolicyName = "login";

        public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        #endregion Public Fields
    }
}
