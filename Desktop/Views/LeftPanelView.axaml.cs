using Avalonia.Controls;
using Avalonia.Input;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class LeftPanelView : UserControl
    {
        #region Public Constructors

        public LeftPanelView() => InitializeComponent();

        #endregion Public Constructors

        #region Private Methods

        // PointerPressed auf dem StackPanel im DataTemplate
        private void OnRoundEntryPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Avalonia.Controls.Control ctrl &&
                ctrl.DataContext is RoundEntryViewModel vm)
                vm.SelectCommand.Execute(null);
        }

        #endregion Private Methods
    }
}