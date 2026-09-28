using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Standings;

namespace PubQuizMaster.Core.Models.Event
{
    public class Quiz
    {
        #region Public Properties

        public Content.Quiz? Content { get; set; }

        public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

        public string? Description { get; set; }

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Number of questions of an imported night, null if unknown. Live nights count their rounds.</summary>
        public int? ImportedQuestionCount { get; set; }

        /// <summary>Number of rounds of an imported night, null if unknown.</summary>
        public int? ImportedRoundCount { get; set; }

        public bool IsLegacyImport { get; set; }

        public List<Participant> ParticipatingTeams { get; set; } = [];

        // Team totals: imported for legacy nights, materialized on completion for live nights
        public List<Result> Results { get; set; } = [];

        // Live quiz structure (empty if IsLegacyImport is true)
        public List<Round> Rounds { get; set; } = [];

        public QuizStatus Status { get; set; } = QuizStatus.Planned;

        // Scorer stations of the night, available before the first round
        public List<ScorerStation> Stations { get; set; } = [];

        public string Title { get; set; } = string.Empty;

        #endregion Public Properties
    }
}
