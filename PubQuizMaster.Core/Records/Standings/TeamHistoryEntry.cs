namespace PubQuizMaster.Core.Records.Standings
{
    /// <summary>
    /// One completed quiz night of a team: its points and place, and the night's field for comparison.
    /// </summary>
    public record TeamHistoryEntry(
        Guid QuizId,
        DateOnly Date,
        string Title,
        decimal Score,
        int? Rank,
        int TeamCount,
        decimal AverageScore);
}
