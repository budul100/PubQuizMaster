namespace PubQuizMaster.Core.Models.Content
{
    public class Question
    {
        #region Public Properties

        public string Answer { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int Position { get; set; }

        public string Text { get; set; } = string.Empty;

        #endregion Public Properties
    }
}