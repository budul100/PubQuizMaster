using System.Collections.Concurrent;
using QRCoder;

namespace PubQuizMaster.Services.Common
{
    /// <summary>
    /// Renders QR codes as PNG data URLs. Singleton with a cache, the scorer URLs stay the same
    /// for a whole quiz night while the dashboard re-renders every few seconds.
    /// </summary>
    public class QrCodeService
    {
        #region Private Fields

        private const int MaxCacheEntries = 256;

        private readonly ConcurrentDictionary<string, string> cache = new(StringComparer.Ordinal);

        #endregion Private Fields

        #region Public Methods

        public string GetQrCodeDataUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            if (cache.TryGetValue(text, out var cached)) return cached;

            // Tokens rotate rarely, a full reset is enough to keep the cache bounded
            if (cache.Count >= MaxCacheEntries)
            {
                cache.Clear();
            }

            return cache.GetOrAdd(text, CreateDataUrl);
        }

        #endregion Public Methods

        #region Private Methods

        private static string CreateDataUrl(string text)
        {
            using var generator = new QRCodeGenerator();

            var data = generator.CreateQrCode(
                plainText: text,
                eccLevel: QRCodeGenerator.ECCLevel.M);

            var qrCode = new PngByteQRCode(data);
            var bytes = qrCode.GetGraphic(6);

            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }

        #endregion Private Methods
    }
}