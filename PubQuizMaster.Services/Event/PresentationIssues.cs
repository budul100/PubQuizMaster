using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Collects template problems while ExportService fills a presentation.
    /// Keeps the order of discovery (deck order) and ignores duplicates.
    /// </summary>
    internal sealed class PresentationIssues
    {
        #region Private Fields

        private readonly List<string> missingShapes = [];
        private readonly List<string> missingSlides = [];
        private readonly List<string> warnings = [];

        #endregion Private Fields

        #region Public Methods

        public void AddMissingShape(string slideName, string shapeName)
        {
            AddUnique(missingShapes, $"{slideName}/{shapeName}");
        }

        public void AddMissingSlide(string slideName)
        {
            AddUnique(missingSlides, slideName);
        }

        public void AddWarning(string message)
        {
            AddUnique(warnings, message);
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
