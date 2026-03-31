using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    // ─────────────────────────────────────────────
    // PAYLOADS
    // Strongly typed DTOs for all hub messages.
    // Separate from domain models to keep hub contracts stable
    // even if internal model naming evolves.
    // ─────────────────────────────────────────────

    public class ScorerConnectedPayload
    {
        public string ScorerId { get; set; } = string.Empty;
        public ScorerAssignment? Assignment { get; set; }
        public RoundSummary? Round { get; set; }
        public List<Team?>? Teams { get; set; }
        public List<Answer>? ExistingAnswers { get; set; }
    }
}