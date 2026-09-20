using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Collects template problems while ExportService fills a presentation.
    /// Keeps the order of discovery (deck order) and ignores duplicates.
    /// </summary>
    public sealed class IssueService
    {
        #region Private Fields

        private readonly List<string> missingShapes = [];
        private readonly List<string> missingSlides = [];
        private readonly List<string> warnings = [];

        #endregion Private Fields

        #region Public Methods

        public void AddMissingShape(string slideName, string shapeName)
        {
            AddUnique(
                target: missingShapes,
                value: $"{slideName}/{shapeName}");
        }

        public void AddMissingSlide(string slideName)
        {
            AddUnique(
                target: missingSlides,
                value: slideName);
        }

        public void AddWarning(string message)
        {
            AddUnique(
                target: warnings,
                value: message);
        }

        public PresentationResult ToResult(PresentationFormat format)
        {
            return new PresentationResult(format, [.. missingSlides], [.. missingShapes], [.. warnings]);
        }

        #endregion Public Methods

        #region Private Methods

        private static void AddUnique(List<string> target, string value)
        {
            if (!target.Contains(value))
            {
                target.Add(value);
            }
        }

        #endregion Private Methods
    }
}