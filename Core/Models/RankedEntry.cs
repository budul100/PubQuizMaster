using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Models
{
    public class RankedEntry
    {
        #region Public Properties

        public int Rank { get; set; }

        public decimal Score { get; set; }

        public Team Team { get; set; } = default!;

        #endregion Public Properties
    }
}