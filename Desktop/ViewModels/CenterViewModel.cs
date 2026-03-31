using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class CenterViewModel
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private ViewModelBase? _currentContent;

        #endregion Private Fields

        #region Public Properties

        // Used by MainWindow to conditionally show the Start button
        public bool IsSetupMode => CurrentContent is SetupViewModel;

        #endregion Public Properties

        #region Public Methods

        public void ShowMatrix(RoundMatrixViewModel vm)
        {
            CurrentContent = vm;
            OnPropertyChanged(nameof(IsSetupMode));
        }

        public void ShowSetup(SetupViewModel vm)
        {
            CurrentContent = vm;
            OnPropertyChanged(nameof(IsSetupMode));
        }

        #endregion Public Methods
    }
}