namespace PubQuizMaster.Desktop.ViewModels
{
    public class LeaderboardEntryViewModel
    {
        #region Public Properties

        public int Rank { get; set; }

        public decimal Score { get; set; }

        public string TeamName { get; set; } = string.Empty;

        #endregion Public Properties
    }
}