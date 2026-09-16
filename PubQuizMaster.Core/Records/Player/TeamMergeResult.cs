namespace PubQuizMaster.Core.Records.Player
{
    /// <summary>
    /// Outcome of merging a duplicate team into a target team.
    /// Dropped entries collided with existing entries of the target team, which take precedence.
    /// </summary>
    public record TeamMergeResult(
        string SourceName,
        string TargetName,
        int MovedAnswers,
        int DroppedAnswers,
        int DroppedResults);
}
