using PubQuizMaster.Core.Enums;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Snapshot of a scorer station's presence and position within a round.
    /// LastSeenUtc is refreshed by every heartbeat, LastChangedUtc only when the position changes.
    /// </summary>
    public record ScorerStatus(
        DateTime LastSeenUtc,
        DateTime LastChangedUtc,
        ScoringType Phase,
        Guid? RoundId,
        int QuestionIndex,
        int TeamIndex);
}
