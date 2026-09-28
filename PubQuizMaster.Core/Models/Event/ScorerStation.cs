namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Scorer station of a quiz night, set up before the first round so scorers can log in early.
    /// Round assignments (Scorer) refer to a station by its ScorerId token.
    /// Starting a round syncs the stations with the round's assignments.
    /// </summary>
    public class ScorerStation
    {
        #region Public Properties

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Label { get; set; } = string.Empty;

        public Guid QuizId { get; set; }

        public string ScorerId { get; set; } = string.Empty;

        #endregion Public Properties
    }
}
