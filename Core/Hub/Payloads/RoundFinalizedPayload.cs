using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundFinalizedPayload
    {
        #region Public Properties

        public List<LeaderboardEntry> Leaderboard { get; set; } = new();

        public Guid RoundId { get; set; }

        #endregion Public Properties
    }
}