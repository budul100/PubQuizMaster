namespace PubQuizMaster.Core.Models.Player
{
    public class Team
    {
        #region Public Properties

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string NormalizedName { get; set; } = string.Empty;

        public List<Result> Results { get; set; } = [];

        #endregion Public Properties
    }
}