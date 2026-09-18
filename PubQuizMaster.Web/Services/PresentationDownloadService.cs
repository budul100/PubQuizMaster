using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
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
        QuizService quizService,
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

            var mode = round.IsFinal ? PresentationMode.Final : PresentationMode.Round;
            await DownloadCoreAsync(quiz, round, mode, sourceFile, ct);
        }

        public async Task DownloadFinalAsync(Guid quizId, IBrowserFile sourceFile, CancellationToken ct = default)
        {
            Quiz? quiz;

            try
            {
                quiz = await quizService.GetQuizAsync(quizId, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Loading quiz night {QuizId} for export failed.", quizId);
                toastService.ShowError($"Export failed: {ex.Message}");
                return;
            }

            if (quiz == null)
            {
                toastService.ShowError("Quiz night not found.");
                return;
            }

            // Prefer round marked as IsFinal, fallback to last finalized round
            var finalRound = quiz.Rounds
                .Where(r => r.IsFinalized && r.IsFinal)
                .OrderBy(r => r.CreatedAt)
                .LastOrDefault()
                ?? quiz.Rounds
                    .Where(r => r.IsFinalized)
                    .OrderBy(r => r.CreatedAt)
                    .LastOrDefault();

            if (finalRound == null)
            {
                toastService.ShowError($"Quiz night '{quiz.Title}' has no finalized round to export.");
                return;
            }

            // The fallback round is not marked final, the closing slides are still wanted here
            await DownloadCoreAsync(quiz, finalRound, PresentationMode.Final, sourceFile, ct);
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

        private static string SanitizeFileNamePart(string value)
        {
            var chars = value.Trim()
                .Select(c => char.IsWhiteSpace(c) || char.IsControl(c) || invalidFileNameChars.Contains(c) ? '_' : c)
                .ToArray();

            var result = new string(chars);
            return string.IsNullOrEmpty(result) ? "Presentation" : result;
        }

        private async Task DownloadCoreAsync(Quiz quiz, Round round, PresentationMode mode,
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

                var format = ExportService.FillPresentation(quiz, round.Id, mode, document);
                var fileName = CreateFileName(sourceFile.Name, format.Extension);

                using var streamReference = new DotNetStreamReference(new MemoryStream(document.ToArray()));
                await js.InvokeVoidAsync("downloadFileFromStream", ct, fileName, format.ContentType, streamReference);

                toastService.ShowSuccess($"Presentation '{fileName}' downloaded.");
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