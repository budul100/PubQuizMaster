namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Outcome of registering a team for the active quiz night.
    /// </summary>
    /// <param name="AddedToOpenRound">True if a round was running and the team joined it.</param>
    /// <param name="ScorerLabel">Scorer the team was assigned to, null if the open round has no scorer.</param>
    public record TeamRegistration(
        string TeamName,
        bool AddedToOpenRound,
        string? ScorerLabel);
}
