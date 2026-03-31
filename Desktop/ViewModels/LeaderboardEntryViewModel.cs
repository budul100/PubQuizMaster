namespace PubQuizMaster.Desktop.ViewModels
{
    public class LeaderboardEntryViewModel
    {
        public int Rank { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }
}
