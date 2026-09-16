using PubQuizMaster.Core.Models.Player;

namespace PubQuizMaster.Core.Records.Player
{
    public record RankedTeam(
        Team Team,
        decimal Score,
        int Rank);
}