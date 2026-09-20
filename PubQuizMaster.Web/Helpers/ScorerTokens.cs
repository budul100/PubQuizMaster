using System.Security.Cryptography;

namespace PubQuizMaster.Web.Helpers
{
    /// <summary>
    /// Creates non-guessable scorer station identifiers used in scorer URLs.
    /// </summary>
    internal static class ScorerTokens
    {
        #region Private Fields

        // No ambiguous characters (0/o, 1/l/i) so tokens can be typed manually
        private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";

        private const int TokenLength = 10;

        #endregion Private Fields

        #region Public Methods

        public static string Create() => RandomNumberGenerator.GetString(
            choices: Alphabet,
            length: TokenLength);

        #endregion Public Methods
    }
}