using Avalonia.Controls;
using Avalonia.Input;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class LeftPanelView 
        : UserControl
    {
        #region Public Constructors

        public LeftPanelView() => InitializeComponent();

        #endregion Public Constructors

        #region Private Methods

        private void OnRoundEntryPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border &&
                border.DataContext is RoundEntryViewModel vm)
                vm.SelectCommand.Execute(null);
        }

        #endregion Private Methods
    }
}