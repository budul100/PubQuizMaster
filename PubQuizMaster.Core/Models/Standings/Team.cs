namespace PubQuizMaster.Core.Models.Standings
{
    public class Team
    {
        #region Public Properties

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Whether the team is out of competition in the all-time standings (e.g. the bar team).
        /// Independent of the per-night flag on the participant.
        /// </summary>
        public bool IsNonCompetitive { get; set; } = false;

        public string Name { get; set; } = string.Empty;

        public string Normalized { get; set; } = string.Empty;

        public List<Result> Results { get; set; } = [];

        #endregion Public Properties
    }
}
