namespace PubQuizMaster.Core.Models.Content
{
    /// <summary>
    /// Questions and answers of a quiz night, taken from the JSON export of the question editor.
    /// Stored as jsonb on the quiz while it is live and dropped on completion.
    /// Title and date of the export live on the quiz itself, see QuizImport.
    /// Always replace the instance, never mutate it: EF detects changes by reference.
    /// Stored as jsonb on the quiz and kept after completion for the read-only view and statistics.
    /// /// </summary>
    public class Quiz
    {
        #region Public Properties

        // Sorted by position after import
        public List<Round> Rounds { get; set; } = [];

        #endregion Public Properties

        #region Public Methods

        public Round? GetRound(int? position)
        {
            return position != null
                ? Rounds.FirstOrDefault(r => r.Position == position)
                : null;
        }

        #endregion Public Methods
    }
}