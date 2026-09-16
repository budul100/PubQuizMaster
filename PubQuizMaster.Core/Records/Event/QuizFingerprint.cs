namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Cheap change indicator for the live quiz night. The dashboard reloads the full graph
    /// only when this differs from the last load.
    /// </summary>
    public record QuizFingerprint(
        Guid QuizId,
        string Title,
        DateOnly Date,
        string? Description,
        int ParticipantCount,
        int RoundCount,
        int FinalizedRoundCount,
        int AssignedTeamSlots,
        int AnswerCount,
        DateTime? LastAnswerAt);
}
