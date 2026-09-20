namespace PubQuizMaster.Services.Common
{
    public class ToastService
    {
        #region Public Events

        public event Action<string, bool>? OnShow;

        #endregion Public Events

        #region Public Methods

        public void ShowError(string message) => OnShow?.Invoke(message, true);

        public void ShowSuccess(string message) => OnShow?.Invoke(message, false);

        #endregion Public Methods
    }
}