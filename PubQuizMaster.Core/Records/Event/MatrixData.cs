using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Player;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Everything the round matrix needs: the round with its answers, the teams of its quiz night
    /// in sheet order, and each team's score from the rounds started before.
    /// </summary>
    public record MatrixData(
        Round Round,
        Team[] Teams,
        Dictionary<Guid, decimal> PriorScores);
}
