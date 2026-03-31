namespace PubQuizMaster.Core.Models.Participants
{
    // ─────────────────────────────────────────────
    // LEADERBOARD PROJECTIONS
    // ─────────────────────────────────────────────

    public class RoundScore
    {
        public Guid RoundId { get; set; }
        public string RoundName { get; set; } = string.Empty;

        /// <summary>Null means the team was not active in this round (display as "–").</summary>
        public decimal? Score { get; set; }
    }
}