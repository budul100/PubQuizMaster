using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Models.Event
{
    public class Quiz
    {
        #region Public Properties

        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public string? Description { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        public bool IsCompleted { get; set; }

        public bool IsLegacyImport { get; set; }

        public List<Participant> ParticipatingTeams { get; set; } = [];

        // Team totals: imported for legacy nights, materialized on completion for live nights
        public List<Result> Results { get; set; } = [];

        // Live quiz structure (empty if IsLegacyImport is true)
        public List<Round> Rounds { get; set; } = [];

        public string Title { get; set; } = string.Empty;

        #endregion Public Properties
    }
}