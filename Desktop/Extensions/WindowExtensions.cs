using System.Threading.Tasks;
using Avalonia.Controls;

namespace PubQuizMaster.Desktop.Extensions
{
    internal static class WindowExtensions
    {
        #region Public Methods

        public static Task HasClosed(this Window window)
        {
            var tcs = new TaskCompletionSource();
            window.Closed += (_, _) => tcs.TrySetResult();
            return tcs.Task;
        }

        #endregion Public Methods
    }
}