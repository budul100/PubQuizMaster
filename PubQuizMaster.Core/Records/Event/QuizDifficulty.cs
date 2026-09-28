using PubQuizMaster.Core.Enums;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Difficulty of a played quiz night: share of correct answers over all its rounds (0 to 1).
    /// Live nights count their recorded rounds, imported nights need their question count.
    /// </summary>
    public record QuizDifficulty(
        Guid QuizId,
        DateOnly Date,
        string Title,
        QuizStatus Status,
        decimal CorrectRate);
}
