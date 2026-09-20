using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Records.Standings
{
    public record RankedTeam(
        Team Team,
        decimal Score,
        int Rank);
}