namespace PubQuizMaster.Core.Models.Content
{
    /// <summary>
    /// One round of the imported questions. A live round refers to it by its position.
    /// </summary>
    public class Round
    {
        #region Public Properties

        public int Position { get; set; }

        // Sorted by position after import, so the list index equals Answer.QuestionIndex
        public List<Question> Questions { get; set; } = [];

        #endregion Public Properties

        #region Public Methods

        /// <summary>Question for a 0-based question index, null if the round has fewer questions.</summary>
        public Question? GetQuestion(int questionIndex)
        {
            return questionIndex >= 0 && questionIndex < Questions.Count
                ? Questions[questionIndex]
                : null;
        }

        #endregion Public Methods
    }
}
