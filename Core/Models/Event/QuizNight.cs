using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Models.Event
{
    public class QuizNight
    {
        #region Public Properties

        public DateTime Date { get; set; } = DateTime.Today;

        public Guid Id { get; set; } = Guid.NewGuid();

        public List<Team> MasterTeamList { get; set; } = [];

        public string Name { get; set; } = string.Empty;

        public List<Round> Rounds { get; set; } = [];

        #endregion Public Properties
    }
}