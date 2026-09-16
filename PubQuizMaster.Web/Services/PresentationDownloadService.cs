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

        private const long DefaultMaxFileSizeMb = 200;
        private const string MaxFileSizeKey = "Export:MaxFileSizeMb";

        // Union of Windows and Unix restrictions, the file is saved on the client
        private static readonly char[] invalidFileNameChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

        #endregion Private Fields

        #region Public Methods

        public async Task DownloadAsync(Quiz quiz, Guid roundId, PresentationMode mode,
            IBrowserFile sourceFile, CancellationToken ct = default)
        {
            var maxFileSize = GetMaxFileSize();
            if (sourceFile.Size > maxFileSize)
            {
                toastService.ShowError($"'{sourceFile.Name}' exceeds the limit of {maxFileSize / 1024 / 1024} MB.");
                return;
            }

            try
            {
                var round = quiz.Rounds.FirstOrDefault(r => r.Id == roundId)
                    ?? throw new InvalidOperationException("Round not found.");

                // BrowserFileStream is not seekable, OpenXml needs random access
                using var document = new MemoryStream();
                await using (var upload = sourceFile.OpenReadStream(maxFileSize, ct))
                {
                    await upload.CopyToAsync(document, ct);
                }

                var format = ExportService.FillPresentation(quiz, roundId, mode, document);
                var fileName = CreateFileName(quiz, round, mode, format.Extension);

                // DotNetStreamReference disposes the stream once the transfer is done
                using var streamReference = new DotNetStreamReference(new MemoryStream(document.ToArray()));
                await js.InvokeVoidAsync("downloadFileFromStream", ct, fileName, format.ContentType, streamReference);

                toastService.ShowSuccess($"Presentation '{fileName}' downloaded.");
            }
            catch (OperationCanceledException)
            {
                // Page was left during the export, nothing to report
            }
            catch (JSDisconnectedException)
            {
                // Circuit is gone, the browser cannot receive the file anymore
            }
            catch (JSException ex)
            {
                logger.LogError(ex, "Browser download failed for round {RoundId}.", roundId);
                toastService.ShowError("Download failed in the browser. Please reload the page (Ctrl+F5).");
            }
            catch (Exception ex) when (ex is FileFormatException or InvalidDataException or OpenXmlPackageException)
            {
                logger.LogWarning(ex, "'{FileName}' is not a valid presentation.", sourceFile.Name);
                toastService.ShowError($"'{sourceFile.Name}' is not a valid PowerPoint file.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Presentation export failed for round {RoundId}.", roundId);
                toastService.ShowError($"Export failed: {ex.Message}");
            }
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

            // Include gives no ordering guarantee
            var lastRound = quiz.Rounds
                .Where(r => r.IsFinalized)
                .OrderBy(r => r.CreatedAt)
                .LastOrDefault();

            if (lastRound == null)
            {
                toastService.ShowError($"Quiz night '{quiz.Title}' has no finalized round to export.");
                return;
            }

            await DownloadAsync(quiz, lastRound.Id, PresentationMode.Final, sourceFile, ct);
        }

        #endregion Public Methods

        #region Private Methods

        private static string CreateFileName(Quiz quiz, Round round, PresentationMode mode, string extension)
        {
            var suffix = mode == PresentationMode.Final ? "_Final" : string.Empty;
            return $"{quiz.Date:yyyy-MM-dd}_{SanitizeFileNamePart(round.Name)}{suffix}{extension}";
        }

        private static string SanitizeFileNamePart(string value)
        {
            var chars = value.Trim()
                .Select(c => char.IsWhiteSpace(c) || char.IsControl(c) || invalidFileNameChars.Contains(c) ? '_' : c)
                .ToArray();

            var result = new string(chars);
            return string.IsNullOrEmpty(result) ? "Round" : result;
        }

        private long GetMaxFileSize()
        {
            var megabytes = configuration.GetValue(MaxFileSizeKey, DefaultMaxFileSizeMb);
            return megabytes * 1024 * 1024;
        }

        #endregion Private Methods
    }
}
