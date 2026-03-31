namespace PubQuizMaster.Desktop.ViewModels
{
    using CommunityToolkit.Mvvm.ComponentModel;
    using PubQuizMaster.Desktop.ViewModels;

    public partial class AnswerCellViewModel : ViewModelBase
    {
        [ObservableProperty] private bool? _isCorrect;
        [ObservableProperty] private bool _isEditing;

        public string Display => IsCorrect switch
        {
            true => "✓",
            false => "✗",
            null => "–"
        };

        public string Color => IsCorrect switch
        {
            true => "#2ecc71",
            false => "#e74c3c",
            null => "#4a4a6a"
        };

        public AnswerCellViewModel(bool? isCorrect) => _isCorrect = isCorrect;

        partial void OnIsCorrectChanged(bool? value) =>
            OnPropertyChanged(nameof(Display));
    }
}
