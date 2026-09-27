using PubQuizMaster.Core.Models.Content;

namespace PubQuizMaster.Core.Records.Import
{
    /// <summary>
    /// JSON quiz export of the question editor, as read from the file.
    /// Title and date go to the quiz, the rounds become its stored content.
    /// </summary>
    public record QuizImport(
        string Title,
        DateOnly? Date,
        List<Round> Rounds)
    {
        #region Public Methods

        public Quiz ToContent() => new() { Rounds = Rounds };

        #endregion Public Methods
    }
}
