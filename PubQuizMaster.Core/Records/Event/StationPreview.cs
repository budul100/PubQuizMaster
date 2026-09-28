using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// What a scorer station sees before its round: its label, the quiz night and the teams it will
    /// most likely score, in sheet order. Preliminary, the start dialog decides the actual assignment.
    /// </summary>
    public record StationPreview(
        string Label,
        string QuizTitle,
        bool IsQuizLive,
        Team[] Teams);
}
