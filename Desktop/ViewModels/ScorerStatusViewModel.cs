namespace PubQuizMaster.Desktop.ViewModels
{
    public class ScorerStatusViewModel
    {
        #region Public Properties

        public int Answered { get; set; }

        public int Expected { get; set; }

        public bool IsComplete => Answered >= Expected;

        public string Label { get; set; } = string.Empty;

        public string Progress => $"{Answered} / {Expected}";

        public string ScorerId { get; set; } = string.Empty;

        #endregion Public Properties
    }
}