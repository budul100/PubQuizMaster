namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// Standing of a team within the active quiz night.
    /// LatestRoundRank is 0 for teams without a score in that round.
    /// </summary>
    public record TeamStanding(
        Guid TeamId,
        string TeamName,
        decimal? LatestRoundScore,
        int LatestRoundRank,
        decimal TotalScore,
        int OverallRank,
        bool IsActive = true,
        bool IsNonCompetitive = false,
        bool CanDelete = false);
}