using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Extensions
{
    /// <summary>
    /// Share of correctly answered questions, the difficulty measure of rounds and quiz nights.
    /// Counted against every sheet of the round, missing answers count as wrong.
    /// </summary>
    public static class CorrectRate
    {
        #region Public Methods

        /// <summary>
        /// Correct answers and possible answers (teams of the round times questions).
        /// Requires Assignments and Answers to be loaded.
        /// </summary>
        public static (decimal Correct, int Possible) GetCorrectCounts(this Round round)
        {
            var correct = round.Answers.Sum(a => a.Value.GetScore());
            var possible = round.GetTeamIds().Length * round.Length;

            return (correct, possible);
        }

        /// <summary>Correct share of the round (0 to 1), null while nothing can be answered.</summary>
        public static decimal? GetCorrectRate(this Round round)
        {
            var (correct, possible) = round.GetCorrectCounts();

            return possible > 0
                ? correct / possible
                : null;
        }

        /// <summary>
        /// Correct share over several rounds, weighted by their possible answers, null if there are none.
        /// </summary>
        public static decimal? GetCorrectRate(this IEnumerable<Round> rounds)
        {
            var counts = rounds
                .Select(r => r.GetCorrectCounts())
                .ToArray();

            var possible = counts.Sum(c => c.Possible);

            return possible > 0
                ? counts.Sum(c => c.Correct) / possible
                : null;
        }

        #endregion Public Methods
    }
}
