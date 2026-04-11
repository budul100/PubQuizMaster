using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using QRCoder;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class ScorerStatusViewModel
    {
        #region Public Constructors

        public ScorerStatusViewModel(string scorerId, string label, int answered, int expected, string url)
        {
            ScorerId = scorerId;
            Label = label;
            Answered = answered;
            Expected = expected;
            Url = url;

            QrCode = GenerateQrCode(url);
            OpenUrlCommand = new RelayCommand(
                execute: () => OpenBrowser(Url),
                canExecute: () => !string.IsNullOrEmpty(Url));
        }

        #endregion Public Constructors

        #region Public Properties

        public int Answered { get; private set; }

        public int Expected { get; private set; }

        public bool IsComplete => Answered >= Expected;

        public string Label { get; private set; } = string.Empty;

        public ICommand OpenUrlCommand { get; }

        public string Progress => $"{Answered} / {Expected}";

        public Bitmap? QrCode { get; private set; }

        public string ScorerId { get; private set; } = string.Empty;

        public string Url { get; private set; } = string.Empty;

        #endregion Public Properties

        #region Private Methods

        private static Bitmap GenerateQrCode(string url)
        {
            using var gen = new QRCodeGenerator();

            var data = gen.CreateQrCode(
                plainText: url,
                eccLevel: QRCodeGenerator.ECCLevel.M);

            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(6);

            using var ms = new System.IO.MemoryStream(bytes);

            return new Bitmap(ms);
        }

        private static void OpenBrowser(string url)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }

        #endregion Private Methods
    }
}