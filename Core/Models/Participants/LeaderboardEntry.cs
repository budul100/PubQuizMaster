namespace PubQuizMaster.Core.Models.Participants
{
    public class LeaderboardEntry
    {
        #region Public Properties

        public int Rank { get; set; }

        public List<RoundScore> ScorePerRound { get; set; } = new();

        public Team Team { get; set; } = new();

        public decimal TotalScore { get; set; }

        #endregion Public Properties
    }
}