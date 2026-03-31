namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Assigns a set of teams (in scoring-sheet order) exclusively to one scorer.
    /// No team may appear in more than one assignment per round.
    /// </summary>
    public class ScorerAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Stable identifier for the scorer session (e.g. device token).</summary>
        public string ScorerId { get; set; } = string.Empty;

        /// <summary>Human-readable label shown in the UI (e.g. "Scorer B – Tisch 2").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of team IDs. Order must match physical sheet order
        /// so the scorer can work top-to-bottom without looking up names.
        /// </summary>
        public List<Guid> TeamIds { get; set; } = new();

        /// <summary>
        /// The question index this scorer is currently working on.
        /// Independent of other scorers — each scorer has their own pace.
        /// </summary>
        public int CurrentQuestionIndex { get; set; } = 0;
    }
}