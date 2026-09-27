namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Start of a new round. All participants of the quiz night take part.
    /// ContentPosition links the round to a round of the imported questions (optional).
    /// </summary>
    public record RoundRequest(
        Guid QuizId,
        string RoundName,
        int Length,
        bool IsFinal,
        List<ScorerRequest> Assignments,
        int? ContentPosition);
}
