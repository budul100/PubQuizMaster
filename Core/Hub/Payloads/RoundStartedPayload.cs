using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundStartedPayload
    {
        #region Public Properties

        public List<Team> AllTeams { get; set; } = [];

        public RoundSummary Round { get; set; } = new();

        #endregion Public Properties
    }
}