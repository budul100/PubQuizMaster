namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Start of a new round. All participants of the quiz night take part.
    /// </summary>
    public record RoundRequest(
        Guid QuizNightId,
        string RoundName,
        int QuestionCount,
        List<AssignmentRequest> Assignments);
}
