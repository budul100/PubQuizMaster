namespace PubQuizMaster.Desktop.ViewModels
{
    public class ScorerStatusViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string ScorerId { get; set; } = string.Empty;
        public int Answered { get; set; }
        public int Expected { get; set; }
        public string Progress => $"{Answered} / {Expected}";
        public bool IsComplete => Answered >= Expected;
    }
}
