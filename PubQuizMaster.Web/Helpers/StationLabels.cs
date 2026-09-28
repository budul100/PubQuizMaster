namespace PubQuizMaster.Web.Helpers
{
    /// <summary>
    /// Default labels for new scorer stations: Scorer A, Scorer B, ... then numbered.
    /// </summary>
    public static class StationLabels
    {
        #region Public Methods

        public static string Next(IEnumerable<string> existingLabels)
        {
            var usedLabels = existingLabels
                .Select(l => l.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var letter = Enumerable.Range('A', 26)
                .Select(c => (char)c)
                .FirstOrDefault(c => !usedLabels.Contains($"Scorer {c}"));

            return letter == default
                ? $"Scorer {usedLabels.Count + 1}"
                : $"Scorer {letter}";
        }

        #endregion Public Methods
    }
}
