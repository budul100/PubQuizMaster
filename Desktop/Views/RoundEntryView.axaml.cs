using Avalonia.Controls;
using Avalonia.Input;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class RoundEntryView : UserControl
    {
        #region Public Constructors

        public RoundEntryView()
        {
            InitializeComponent();
        }

        #endregion Public Constructors

        #region Private Methods

        private void OnRoundEntryPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is RoundEntryViewModel vm)
            {
                vm.SelectCommand.Execute(null);
            }
        }

        #endregion Private Methods
    }
}