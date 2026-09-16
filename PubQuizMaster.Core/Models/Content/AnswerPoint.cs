namespace PubQuizMaster.Core.Models.Content
{
    /// <summary>Point-based answer for future use (e.g. partial credit).</summary>
    public class AnswerPoint
        : AnswerBase
    {
        #region Public Properties

        public decimal Points { get; set; }

        #endregion Public Properties

        #region Public Methods

        public override decimal GetScore() => Points;

        #endregion Public Methods
    }
}