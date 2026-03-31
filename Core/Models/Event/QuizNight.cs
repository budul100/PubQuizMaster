using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// The top-level container for an entire quiz evening.
    /// Persisted as a single JSON file.
    /// </summary>
    public class QuizNight
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;       // e.g. "Pub Quiz – 28.03.2026"
        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>
        /// Master list of all teams that ever participated this evening.
        /// Teams are never deleted — they are deactivated per round.
        /// </summary>
        public List<Team> MasterTeamList { get; set; } = new();

        /// <summary>All rounds in chronological order.</summary>
        public List<Round> Rounds { get; set; } = new();


        // ── Cross-round aggregation ───────────────

        /// <summary>
        /// Total score for one team across all rounds.
        /// Rounds where the team was not active contribute 0 (not null).
        /// </summary>
        public decimal GetTotalScore(Guid teamId)
        {
            return Rounds.Sum(r => r.GetTeamScore(teamId) ?? 0m);
        }

        /// <summary>
        /// Full leaderboard for the entire evening, sorted by total score descending.
        /// Returns one entry per team that was active in at least one round.
        /// </summary>
        public List<LeaderboardEntry> GetLeaderboard()
        {
            var activeTeamIds = Rounds
                .SelectMany(r => r.ActiveTeamIds)
                .Distinct();

            return activeTeamIds
                .Select(teamId => new LeaderboardEntry
                {
                    Team = MasterTeamList.First(t => t.Id == teamId),
                    TotalScore = GetTotalScore(teamId),
                    ScorePerRound = Rounds
                        .Select(r => new RoundScore
                        {
                            RoundId = r.Id,
                            RoundName = r.Name,
                            Score = r.GetTeamScore(teamId)   // null if not active
                        })
                        .ToList()
                })
                .OrderByDescending(e => e.TotalScore)
                .ToList();
        }

        /// <summary>
        /// Returns the ScorerAssignment for a given scorer ID in the active round.
        /// Returns null if the scorer is not assigned.
        /// </summary>
        public ScorerAssignment? GetAssignment(Guid roundId, string scorerId)
        {
            return Rounds
                .FirstOrDefault(r => r.Id == roundId)?
                .Assignments
                .FirstOrDefault(a => a.ScorerId == scorerId);
        }
    }
}