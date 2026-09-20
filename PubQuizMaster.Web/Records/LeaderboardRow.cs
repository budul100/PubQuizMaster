using PubQuizMaster.Core.Records.Player;

namespace PubQuizMaster.Web.Records
{
    /// <summary>One line of the all-time standings with its rank for the selected sort criterion.</summary>
    public record LeaderboardRow(
        LeaderboardTeam Team,
        int Rank);
}
