using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundStartedPayload
    {
        public RoundSummary Round { get; set; } = new();
        public List<Team> AllTeams { get; set; } = new();
    }
}