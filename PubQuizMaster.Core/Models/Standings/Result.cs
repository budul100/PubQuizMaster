using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Models.Standings
{
    /// <summary>
    /// Total score of a team for one quiz night. Imported for legacy nights,
    /// materialized by LiveResultBuilder when a live night is completed.
    /// </summary>
    public class Result
    {
        #region Public Properties

        public Guid Id { get; set; } = Guid.NewGuid();

        public Quiz Quiz { get; set; } = null!;

        public Guid QuizId { get; set; }

        public int? Rank { get; set; }

        public Team Team { get; set; } = null!;

        public Guid TeamId { get; set; }

        public decimal TotalScore { get; set; }

        #endregion Public Properties
    }
}