namespace PubQuizMaster.Data
{
    /// <summary>
    /// Database names of constraints the application reacts to.
    /// </summary>
    public static class DbConstraintNames
    {
        #region Public Fields

        public const string AnswerCell = "IX_Answers_RoundId_TeamId_QuestionIndex";

        public const string ResultPerTeam = "IX_Scores_QuizId_TeamId";

        public const string SingleActiveQuiz = "IX_Quizzes_SingleActive";

        public const string TeamName = "IX_Teams_NormalizedName";

        #endregion Public Fields
    }
}
