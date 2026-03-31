using PubQuizMaster.Core.Models.Contents;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// One quiz round (e.g. "Round 3 – Geography").
    /// Rounds are self-contained: team list, scorer assignments, and answers
    /// all belong to the round. Different rounds can have different team sets.
    /// </summary>
    public class Round
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFinalized { get; set; } = false;

        /// <summary>
        /// Teams active in this round. May differ from the master team list:
        /// new teams can join, existing teams can drop out.
        /// </summary>
        public List<Guid> ActiveTeamIds { get; set; } = new();

        /// <summary>
        /// Exclusive scorer assignments. Validated on creation:
        /// no team ID may appear in more than one assignment.
        /// </summary>
        public List<ScorerAssignment> Assignments { get; set; } = new();

        /// <summary>All recorded answers for this round.</summary>
        public List<Answer> Answers { get; set; } = new();


        // ── Convenience methods ───────────────────

        /// <summary>
        /// Returns the score for one team in this round.
        /// Teams not active in this round return null (shown as "–", not 0).
        /// </summary>
        public decimal? GetTeamScore(Guid teamId)
        {
            if (!ActiveTeamIds.Contains(teamId)) return null;

            return Answers
                .Where(a => a.TeamId == teamId)
                .Sum(a => a.Value.GetScore());
        }

        /// <summary>
        /// Returns the score for one question across all active teams.
        /// Useful for per-question statistics.
        /// </summary>
        public decimal GetQuestionScore(int questionIndex)
        {
            return Answers
                .Where(a => a.QuestionIndex == questionIndex)
                .Sum(a => a.Value.GetScore());
        }

        /// <summary>
        /// Validates that no team is assigned to more than one scorer.
        /// Call this before saving a new assignment configuration.
        /// </summary>
        public bool ValidateAssignments(out string error)
        {
            var allTeamIds = Assignments.SelectMany(a => a.TeamIds).ToList();
            var duplicates = allTeamIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                error = $"Duplicate team assignments: {string.Join(", ", duplicates)}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool? GetAnswer(Guid teamId, int questionIndex)
        {
            var a = Answers.FirstOrDefault(x => x.TeamId == teamId && x.QuestionIndex == questionIndex);
            if (a == null) return null;
            return a.Value.GetScore() > 0;
        }

    }
}