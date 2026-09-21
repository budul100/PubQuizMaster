using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Web.Components.Event
{
    /// <summary>
    /// Editable scorer station in the start-round dialog. Decoupled from the Scorer entity.
    /// </summary>
    public class ScorerAssignment
    {
        #region Public Properties

        public string Label { get; set; } = string.Empty;

        public string ScorerId { get; set; } = string.Empty;

        /// <summary>Team IDs in sheet order, mutable for the checkbox toggles.</summary>
        public List<Guid> TeamIds { get; set; } = [];

        #endregion Public Properties

        #region Public Methods

        public ScorerRequest ToRequest()
        {
            return new ScorerRequest(ScorerId.Trim(), Label.Trim(), [.. TeamIds]);
        }

        #endregion Public Methods
    }
}