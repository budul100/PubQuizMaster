using System.Globalization;
using System.Reflection;

namespace PubQuizMaster.Web.Helpers
{
    /// <summary>
    /// Build stamp shown at the bottom of the sidebar.
    /// Source: last write time of the entry assembly (variant A). The Docker build writes the
    /// assembly anew on every build with code changes, so the stamp equals the build time.
    /// Caveat: Assembly.Location is empty for single-file publish, the stamp then reads "unknown".
    /// Shown in UTC because the server runs in UTC; no time zone data is required in the image.
    /// </summary>
    public static class AppVersion
    {
        #region Public Properties

        public static string Current { get; } = CreateStamp();

        #endregion Public Properties

        #region Private Methods

        private static string CreateStamp()
        {
            var location = Assembly.GetEntryAssembly()?.Location;

            if (string.IsNullOrEmpty(location) || !File.Exists(location))
            {
                return "unknown";
            }

            var stamp = File.GetLastWriteTimeUtc(location);
            return stamp.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        }

        #endregion Private Methods
    }
}