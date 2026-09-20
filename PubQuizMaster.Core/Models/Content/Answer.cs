namespace PubQuizMaster.Core.Models.Content
{
    /// <summary>
    /// A single recorded answer for one team on one question within a round.
    /// </summary>
    public class Answer
    {
        #region Public Properties

        public Guid Id { get; set; } = Guid.NewGuid();

        // 0-based
        public int QuestionIndex { get; set; }

        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

        public Guid RoundId { get; set; }

        public string ScorerId { get; set; } = string.Empty;

        public Guid TeamId { get; set; }

        public AnswerBase Value { get; set; } = new AnswerBool();

        #endregion Public Properties
    }
}