using PubQuizMaster.Core.Models.Content;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Edited title, date, description and imported questions of a quiz night.
    /// Content replaces the stored questions, null removes them.
    /// </summary>
    public record QuizDetails(
        Guid QuizId,
        string Title,
        DateOnly Date,
        string? Description,
        Quiz? Content);
}
