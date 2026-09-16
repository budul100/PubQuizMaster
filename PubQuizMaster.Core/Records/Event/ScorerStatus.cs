using PubQuizMaster.Core.Enums;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Snapshot of a scorer station's presence and position within a round.
    /// </summary>
    public record ScorerStatus(
        DateTime LastSeenUtc,
        ScorerPhase Phase,
        Guid? RoundId,
        int QuestionIndex,
        int TeamIndex);
}