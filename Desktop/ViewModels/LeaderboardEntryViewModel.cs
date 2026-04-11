namespace PubQuizMaster.Desktop.ViewModels
{
    public class LeaderboardEntryViewModel(int rank, decimal score, string teamName)
    {
        #region Public Properties

        public int Rank { get; } = rank;

        public decimal Score { get; } = score;

        public string TeamName { get; } = teamName;

        #endregion Public Properties
    }
}