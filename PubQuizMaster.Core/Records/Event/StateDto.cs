using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// State of a scorer station. Assignment and Round are set while the station scores an open round,
    /// Preview while it waits for one (null if the token belongs to no station of a planned or live night).
    /// </summary>
    public record StateDto(
        Scorer? Assignment,
        Models.Event.Round? Round,
        List<Team> AssignedTeams,
        List<Answer> ExistingAnswers,
        Models.Content.Round? Content = null,
        StationPreview? Preview = null);
}
