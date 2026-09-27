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

        public List<Answer> Answers { get; set; } = [];

        public List<Scorer> Assignments { get; set; } = [];

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsFinal { get; set; } = false;

        public bool IsFinalized { get; set; } = false;

        public int Length { get; set; }

        public string Name { get; set; } = string.Empty;

        public int? Position { get; set; }

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

        /// <summary>
        /// Scorer stations with at least one assigned sheet that is not fully recorded yet.
        /// Stations whose sheets are complete (e.g. finished via the matrix) are done, whatever their phase.
        /// Requires Assignments and Answers to be loaded.
        /// </summary>
        public string[] GetPendingScorerIds()
        {
            var answerCounts = Answers
                .GroupBy(a => a.TeamId)
                .ToDictionary(g => g.Key, g => g.Count());

            return Assignments
                .Where(a => a.TeamIds.Any(teamId => answerCounts.GetValueOrDefault(teamId) < Length))
                .Select(a => a.ScorerId)
                .ToArray();
        }

        #endregion Public Methods
    }
}
