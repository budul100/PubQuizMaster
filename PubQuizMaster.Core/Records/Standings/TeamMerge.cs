namespace PubQuizMaster.Core.Records.Standings
{
    /// <summary>
    /// Outcome of merging a duplicate team into a target team.
    /// Dropped entries collided with existing entries of the target team, which take precedence.
    /// </summary>
    public record TeamMerge(
        string SourceName,
        string TargetName,
        int MovedAnswers,
        int DroppedAnswers,
        int DroppedResults);
}