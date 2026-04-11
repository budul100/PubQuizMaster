namespace PubQuizMaster.Core.Models.Participants
{
    public class RoundScore
    {
        #region Public Properties

        public Guid RoundId { get; set; }

        public string RoundName { get; set; } = string.Empty;

        /// <summary>Null means the team was not active in this round (display as "–").</summary>
        public decimal? Score { get; set; }

        #endregion Public Properties
    }
}