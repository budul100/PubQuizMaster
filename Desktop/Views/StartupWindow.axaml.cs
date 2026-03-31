using Avalonia.Controls;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class StartupWindow
        : Window
    {
        #region Public Constructors

        public StartupWindow()
        {
            InitializeComponent();
        }

        public StartupWindow(StartupViewModel vm) 
            : this()
        {
            DataContext = vm;
            vm.CloseRequested = Close;
        }

        #endregion Public Constructors
    }
}