namespace PubQuizMaster.Core.Records.Event
{
    public record ScorerRequest(
        string ScorerId,
        string Label,
        List<Guid> TeamIds);
}