using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Everything the round matrix needs: the round with its answers, the teams of its quiz night
    /// in sheet order, and each team's score from the rounds started before.
    /// </summary>
    public record MatrixData(
        Round Round,
        MatrixTeam[] Teams,
        Dictionary<Guid, decimal> PriorScores);
}