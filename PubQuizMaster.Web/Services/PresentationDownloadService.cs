using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using DocumentFormat.OpenXml.Packaging;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Event;

namespace PubQuizMaster.Web.Services
{
    /// <summary>
    /// Fills an uploaded presentation with the round results and hands it back to the browser.
    /// Scoped, so IJSRuntime and ToastService belong to the calling circuit.
    /// </summary>
    public class PresentationDownloadService(
        IConfiguration configuration,
        IJSRuntime js,
        ILogger<PresentationDownloadService> logger,
        ToastService toastService)
    {
        #region Private Fields

        private const string AdjustedSuffix = "_adjusted";
        private const long DefaultMaxFileSizeMb = 200;
        private const string MaxFileSizeKey = "Export:MaxFileSizeMb";

        private static readonly char[] invalidFileNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

        #endregion Private Fields

        #region Public Methods

        public async Task DownloadAsync(Quiz quiz, Guid roundId, IBrowserFile sourceFile,
            CancellationToken ct = default)
        {
            var round = quiz.Rounds.FirstOrDefault(r => r.Id == roundId);

            if (round == null)
            {
                toastService.ShowError("Round not found.");
                return;
            }

            var mode = round.IsFinal ? PresentationType.Final : PresentationType.Round;
            await DownloadCoreAsync(quiz, round, mode, sourceFile, ct);
        }

        #endregion Public Methods

        #region Private Methods

        private static string CreateFileName(string sourceFileName, string extension)
        {
            var baseName = Path.GetFileNameWithoutExtension(sourceFileName);
            if (!baseName.EndsWith(AdjustedSuffix, StringComparison.OrdinalIgnoreCase))
            {
                baseName += AdjustedSuffix;
            }

            var cleanName = SanitizeFileNamePart(baseName);
            return $"{cleanName}{extension}";
        }

        private static string FormatIssues(PresentationResult result)
        {
            var parts = new List<string>();

            if (result.MissingSlides.Length > 0)
            {
                parts.Add($"Missing slides: {string.Join(", ", result.MissingSlides)}");
            }

            if (result.MissingShapes.Length > 0)
            {
                parts.Add($"Missing shapes: {string.Join(", ", result.MissingShapes)}");
            }

            parts.AddRange(result.Warnings);

            return "The template is incomplete, these parts were skipped. " + string.Join(" · ", parts);
        }

        private static string SanitizeFileNamePart(string value)
        {
            var chars = value.Trim()
                .Select(c => char.IsWhiteSpace(c) || char.IsControl(c) || invalidFileNameChars.Contains(c) ? '_' : c)
                .ToArray();

            var result = new string(chars);
            return string.IsNullOrEmpty(result) ? "Presentation" : result;
        }

        private async Task DownloadCoreAsync(Quiz quiz, Round round, PresentationType mode,
            IBrowserFile sourceFile, CancellationToken ct)
        {
            var maxFileSize = GetMaxFileSize();
            if (sourceFile.Size > maxFileSize)
            {
                toastService.ShowError($"'{sourceFile.Name}' exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
                return;
            }

            try
            {
                using var document = new MemoryStream();
                await using (var upload = sourceFile.OpenReadStream(maxFileSize, ct))
                {
                    await upload.CopyToAsync(document, ct);
                }

                var result = ExportService.FillPresentation(quiz, round.Id, mode, document);
                var fileName = CreateFileName(sourceFile.Name, result.Format.Extension);

                // Hand the filled stream over directly instead of copying it once more (S8).
                // InvokeVoidAsync returns only after the browser has read the stream,
                // so disposing the document afterwards is safe.
                document.Position = 0;
                using var streamReference = new DotNetStreamReference(document, leaveOpen: true);
                await js.InvokeVoidAsync("downloadFileFromStream", ct, fileName, result.Format.ContentType, streamReference);

                toastService.ShowSuccess($"Presentation '{fileName}' downloaded.");

                if (result.HasIssues)
                {
                    var message = FormatIssues(result);
                    logger.LogWarning("Presentation export for round {RoundId}: {Issues}", round.Id, message);
                    toastService.ShowError(message);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
            catch (JSException ex)
            {
                logger.LogError(ex, "Browser download failed for round {RoundId}.", round.Id);
                toastService.ShowError("Download failed in the browser. Please reload the page (Ctrl+F5).");
            }
            catch (Exception ex) when (ex is FileFormatException or InvalidDataException or OpenXmlPackageException)
            {
                logger.LogWarning(ex, "'{FileName}' is not a valid presentation.", sourceFile.Name);
                toastService.ShowError($"'{sourceFile.Name}' is not a valid PowerPoint file.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Presentation export failed for round {RoundId}.", round.Id);
                toastService.ShowError($"Export failed: {ex.Message}");
            }
        }

        private long GetMaxFileSize()
        {
            var megabytes = configuration.GetValue(MaxFileSizeKey, DefaultMaxFileSizeMb);
            return megabytes * 1024 * 1024;
        }

        #endregion Private Methods
    }
}