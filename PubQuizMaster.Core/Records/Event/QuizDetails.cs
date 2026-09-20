namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Edited title, date and description of a quiz night.
    /// </summary>
    public record QuizDetails(
        Guid QuizId,
        string Title,
        DateOnly Date,
        string? Description);
}
