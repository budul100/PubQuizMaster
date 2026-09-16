namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Assigns a set of teams (in scoring-sheet order) exclusively to one scorer.
    /// No team may appear in more than one assignment per round.
    /// </summary>
    public class Scorer
    {
        #region Public Properties

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Human-readable label shown in the UI (e.g. "Scorer B – Tisch 2").</summary>
        public string Label { get; set; } = string.Empty;

        public Guid RoundId { get; set; }

        /// <summary>Stable identifier for the scorer session (e.g. device token).</summary>
        public string ScorerId { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of team IDs. Order must match physical sheet order
        /// so the scorer can work top-to-bottom without looking up names.
        /// </summary>
        public List<Guid> TeamIds { get; set; } = new();

        #endregion Public Properties
    }
}