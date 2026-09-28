using PubQuizMaster.Core.Enums;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Cheap change indicator of a quiz night. The detail page reloads the full graph
    /// only when this differs from the last load.
    /// </summary>
    public record QuizFingerprint(
        Guid QuizId,
        QuizStatus Status,
        string Title,
        DateOnly Date,
        string? Description,
        int ParticipantCount,
        int ActiveParticipantCount,
        int NonCompetitiveCount,
        int StationCount,
        int RoundCount,
        int FinalizedRoundCount,
        Guid? FinalRoundId,
        int AssignedTeamSlots,
        int AnswerCount,
        DateTime? LastAnswerAt);
}
