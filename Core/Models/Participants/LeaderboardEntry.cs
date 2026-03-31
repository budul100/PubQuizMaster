namespace PubQuizMaster.Core.Models.Participants
{
    public class LeaderboardEntry
    {
        public Team Team { get; set; } = new();
        public decimal TotalScore { get; set; }
        public List<RoundScore> ScorePerRound { get; set; } = new();
        public int Rank { get; set; }   // set by caller after sorting
    }
}