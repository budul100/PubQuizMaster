using PubQuizMaster.Core.Models.Player;

namespace PubQuizMaster.Core.Records.Player
{
    public record TeamMatch(
        Team Team,
        double Similarity);
}