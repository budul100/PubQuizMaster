using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Join entity linking teams to a live quiz night with sheet ordering and night-specific status.
    /// </summary>
    public class Participant
    {
        #region Public Properties

        /// <summary>Whether the team participates actively in new rounds of this quiz night.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Whether the team plays out of competition (außer Konkurrenz) for this quiz night.</summary>
        public bool IsNonCompetitive { get; set; } = false;

        public Quiz Quiz { get; set; } = null!;

        public Guid QuizId { get; set; }

        public int SheetOrder { get; set; }

        public Team Team { get; set; } = null!;

        public Guid TeamId { get; set; }

        #endregion Public Properties
    }
}