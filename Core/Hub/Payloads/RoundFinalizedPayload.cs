using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundFinalizedPayload
    {
        public Guid RoundId { get; set; }
        public List<LeaderboardEntry> Leaderboard { get; set; } = new();
    }
}