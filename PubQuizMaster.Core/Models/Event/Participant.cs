using PubQuizMaster.Core.Models.Player;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Join entity linking teams to a live quiz night with sheet ordering.
    /// </summary>
    public class Participant
    {
        #region Public Properties

        public Quiz Quiz { get; set; } = null!;

        public Guid QuizId { get; set; }

        public int SheetOrder { get; set; }

        public Team Team { get; set; } = null!;

        public Guid TeamId { get; set; }

        #endregion Public Properties
    }
}