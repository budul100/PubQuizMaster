namespace PubQuizMaster.Core.Models.Contents
{
    /// <summary>
    /// A single recorded answer for one team on one question within a round.
    /// </summary>
    public class Answer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TeamId { get; set; }
        public int QuestionIndex { get; set; }      // 0-based
        public AnswerBase Value { get; set; } = new AnswerBool();
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public string RecordedByScorerId { get; set; } = string.Empty;
    }
}