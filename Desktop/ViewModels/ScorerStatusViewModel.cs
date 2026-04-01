using Avalonia.Media.Imaging;
using QRCoder;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class ScorerStatusViewModel
    {
        #region Public Properties

        public int Answered { get; set; }

        public int Expected { get; set; }

        public bool IsComplete => Answered >= Expected;

        public string Label { get; set; } = string.Empty;

        public string Progress => $"{Answered} / {Expected}";

        public Bitmap? QrCode { get; set; }

        public string ScorerId { get; set; } = string.Empty;

        #endregion Public Properties

        #region Public Methods

        public static Bitmap GenerateQrCode(string url)
        {
            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(6);
            using var ms = new System.IO.MemoryStream(bytes);
            return new Bitmap(ms);
        }

        #endregion Public Methods
    }
}