namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// A team row source for the round matrix, including its out-of-competition status for the night.
    /// </summary>
    public record MatrixTeam(
        Guid TeamId,
        string Name,
        bool IsNonCompetitive);
}