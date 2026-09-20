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

        public string Label { get; set; } = string.Empty;

        public Guid RoundId { get; set; }

        public string ScorerId { get; set; } = string.Empty;

        public List<Guid> TeamIds { get; set; } = new();

        #endregion Public Properties
    }
}