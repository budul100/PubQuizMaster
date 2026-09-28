using System.Globalization;

namespace PubQuizMaster.Core.Extensions
{
    /// <summary>
    /// The one comparer for ordering team names: scoring sheets, scorer stations, lists and tie orders.
    /// Fixed to German collation, the server culture of a container is usually invariant
    /// and would sort umlauts differently than the sheets on the table.
    /// </summary>
    public static class TeamNameComparer
    {
        #region Public Properties

        public static StringComparer Instance { get; } = StringComparer.Create(
            culture: CultureInfo.GetCultureInfo("de-DE"),
            ignoreCase: true);

        #endregion Public Properties
    }
}
