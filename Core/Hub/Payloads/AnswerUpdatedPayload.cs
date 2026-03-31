using PubQuizMaster.Core.Models.Contents;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class AnswerUpdatedPayload
    {
        public Guid TeamId { get; set; }
        public int QuestionIndex { get; set; }
        public AnswerBase Value { get; set; } = default!;
        public string ScoredBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}