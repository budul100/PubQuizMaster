using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Records.Event
{
    public record StateDto(
        Scorer? Assignment,
        Round? Round,
        List<Team> AssignedTeams,
        List<Answer> ExistingAnswers);
}