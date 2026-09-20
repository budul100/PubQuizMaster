namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Start of a new round. All participants of the quiz night take part.
    /// </summary>
    public record RoundRequest(
        Guid QuizId,
        string RoundName,
        int Length,
        bool IsFinal,
        List<ScorerRequest> Assignments);
}