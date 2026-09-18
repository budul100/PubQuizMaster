namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Start of a new round. All participants of the quiz night take part.
    /// </summary>
    public record RoundRequest(
        Guid QuizNightId,
        string RoundName,
        int QuestionCount,
        bool IsFinal,
        List<AssignmentRequest> Assignments);
}