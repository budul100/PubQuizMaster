namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// Standing of a team within the active quiz night.
    /// </summary>
    public record TeamStanding(
        Guid TeamId,
        string TeamName,
        decimal? LatestRoundScore,
        decimal TotalScore,
        int OverallRank,
        bool IsActive = true,
        bool IsNonCompetitive = false,
        bool CanDelete = false);
}