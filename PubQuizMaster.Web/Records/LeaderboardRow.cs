using PubQuizMaster.Core.Records.Standings;

namespace PubQuizMaster.Web.Records
{
    /// <summary>One line of the standings with its rank for the selected sort criterion.</summary>
    public record LeaderboardRow(
        LeaderboardTeam Team,
        int Rank);
}
