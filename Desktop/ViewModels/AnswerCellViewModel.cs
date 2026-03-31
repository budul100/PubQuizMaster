using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class AnswerCellViewModel(bool? isCorrect)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool? _isCorrect = isCorrect;
        [ObservableProperty] private bool _isEditing;

        #endregion Private Fields

        #region Public Properties

        public string Color => IsCorrect switch
        {
            true => "#2ecc71",
            false => "#e74c3c",
            null => "#4a4a6a"
        };

        public string Display => IsCorrect switch
        {
            true => "✓",
            false => "✗",
            null => "–"
        };

        #endregion Public Properties

        #region Private Methods

        partial void OnIsCorrectChanged(bool? value)
        {
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(Color));
        }

        #endregion Private Methods
    }
}