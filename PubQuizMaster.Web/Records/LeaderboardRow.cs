using PubQuizMaster.Core.Records.Standings;

namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// One line of the standings. Every column carries its own badge, so every metric is ranked.
    /// AverageRank is 0 for teams below the minimum number of quiz nights.
    /// </summary>
    public record LeaderboardRow(
        LeaderboardTeam Team,
        int QuizzesRank,
        int TotalRank,
        int AverageRank);
}