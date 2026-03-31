using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public bool IsFinalized { get; set; }

        public static RoundSummary From(Round round) => new()
        {
            Id = round.Id,
            Name = round.Name,
            QuestionCount = round.QuestionCount,
            IsFinalized = round.IsFinalized
        };
    }
}