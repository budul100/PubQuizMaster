using PubQuizMaster.Core.Models.Content;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// One quiz round (e.g. "Round 3 - Geography").
    /// Rounds are self-contained: scorer assignments and answers belong to the round.
    /// The teams of a round are the teams assigned to its scorers or having recorded answers.
    /// </summary>
    public class Round
    {
        #region Public Properties

        /// <summary>All recorded answers for this round.</summary>
        public List<Answer> Answers { get; set; } = new();

        /// <summary>
        /// Exclusive scorer assignments. Validated by QuizService.StartRoundAsync:
        /// every team appears in exactly one assignment.
        /// </summary>
        public List<Scorer> Assignments { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsFinalized { get; set; } = false;

        public string Name { get; set; } = string.Empty;

        public int QuestionCount { get; set; }

        public Guid QuizId { get; set; }

        #endregion Public Properties

        #region Public Methods

        /// <summary>
        /// Teams taking part in this round, derived from scorer assignments and recorded answers.
        /// Requires Assignments and Answers to be loaded.
        /// </summary>
        public Guid[] GetTeamIds()
        {
            var assignmentTeamIds = Assignments.SelectMany(a => a.TeamIds);
            var answerTeamIds = Answers.Select(a => a.TeamId);

            return assignmentTeamIds.Union(answerTeamIds).ToArray();
        }

        #endregion Public Methods
    }
}