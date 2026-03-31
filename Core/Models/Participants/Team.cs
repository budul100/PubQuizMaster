namespace PubQuizMaster.Core.Models.Participants
{
    // ─────────────────────────────────────────────
    // CORE ENTITIES
    // ─────────────────────────────────────────────

    /// <summary>
    /// A team participating in the quiz night.
    /// Teams persist across all rounds — they are activated per round.
    /// </summary>
    public class Team
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Physical order of the scoring sheets — scorers sort their
        /// answer sheets by this number before starting.
        /// </summary>
        public int SheetOrder { get; set; }
    }
}