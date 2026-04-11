using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PubQuizMaster.Desktop.Behaviors
{
    public static class EnterKeyCommandBehavior
    {
        #region Public Fields

        public static readonly AttachedProperty<ICommand?> CommandProperty = AvaloniaProperty
            .RegisterAttached<Control, ICommand?>(
                name: "Command",
                ownerType: typeof(EnterKeyCommandBehavior));

        #endregion Public Fields

        #region Public Constructors

        static EnterKeyCommandBehavior()
        {
            CommandProperty.Changed.AddClassHandler<Control>(OnCommandChanged);
        }

        #endregion Public Constructors

        #region Public Methods

        public static ICommand? GetCommand(AvaloniaObject element) => element.GetValue(CommandProperty);

        public static void SetCommand(AvaloniaObject element, ICommand? value) => element.SetValue(CommandProperty, value);

        #endregion Public Methods

        #region Private Methods

        private static void OnCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            control.KeyDown -= OnKeyDown;

            if (e.NewValue is ICommand)
            {
                control.KeyDown += OnKeyDown;
            }
        }

        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter
                && sender is Control control)
            {
                var command = GetCommand(control);

                if (command?.CanExecute(null) == true)
                {
                    command.Execute(null);
                    e.Handled = true;
                }
            }
        }

        #endregion Private Methods
    }
}