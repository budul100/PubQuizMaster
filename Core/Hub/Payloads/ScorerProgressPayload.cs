namespace PubQuizMaster.Core.Hub.Payloads
{
    public class ScorerProgressPayload
    {
        public string ScorerId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int CurrentQuestionIndex { get; set; }
    }
}