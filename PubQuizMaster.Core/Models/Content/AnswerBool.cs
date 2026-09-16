namespace PubQuizMaster.Core.Models.Content
{
    /// <summary>Boolean answer: correct = 1 point, incorrect = 0 points.</summary>
    public class AnswerBool
        : AnswerBase
    {
        #region Public Properties

        public bool Correct { get; set; }

        #endregion Public Properties

        #region Public Methods

        public override decimal GetScore() => Correct ? 1m : 0m;

        #endregion Public Methods
    }
}