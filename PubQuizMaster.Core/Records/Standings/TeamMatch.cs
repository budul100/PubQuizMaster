using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Records.Standings
{
    public record TeamMatch(
        Team Team,
        double Similarity);
}