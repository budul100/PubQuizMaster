namespace PubQuizMaster.Core.Records.Event
{
    public record AssignmentRequest(
        string ScorerId,
        string Label,
        List<Guid> TeamIds);
}