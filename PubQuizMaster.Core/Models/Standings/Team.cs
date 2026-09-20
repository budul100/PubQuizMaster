namespace PubQuizMaster.Core.Models.Standings
{
    public class Team
    {
        #region Public Properties

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string Normalized { get; set; } = string.Empty;

        public List<Result> Results { get; set; } = [];

        #endregion Public Properties
    }
}