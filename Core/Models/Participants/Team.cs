namespace PubQuizMaster.Core.Models.Participants
{
    public class Team
    {
        #region Public Properties

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public int SheetOrder { get; set; }

        #endregion Public Properties
    }
}