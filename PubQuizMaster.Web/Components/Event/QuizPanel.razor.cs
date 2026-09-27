using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Services.Import;

namespace PubQuizMaster.Web.Components.Event
{
    /// <summary>
    /// Start page while no quiz is active: list of completed quizzes and imports.
    /// Title, date and description are edited only in the live dashboard (QuizEditModal).
    /// </summary>
    public partial class QuizPanel
    {
        #region Private Fields

        private int inputVersion;
        private bool isSubmitting;
        private Quiz? quizToDelete;
        private Quiz? quizToReopen;
        private bool showDeleteModal;
        private bool showReopenModal;

        #endregion Private Fields

        #region Public Properties

        /// <summary>Raised after a quiz night was created or deleted. The page reloads its state.</summary>
        [Parameter] public EventCallback OnDataChanged { get; set; }

        /// <summary>
        /// Raised after a quiz night was reopened. The page notifies the other circuits and reloads,
        /// so the round change is announced once and loaded once.
        /// </summary>
        [Parameter] public EventCallback OnQuizReopened { get; set; }

        [Parameter] public List<Quiz> QuizNights { get; set; } = [];

        #endregion Public Properties

        #region Private Methods

        private static string GetDefaultTitle(DateOnly date) => $"Pub Quiz ({date:dd.MM.yyyy})";

        private async Task CreateQuizNightFromFileAsync(InputFileChangeEventArgs e)
        {
            if (isSubmitting) return;

            isSubmitting = true;
            try
            {
                await using var stream = e.File.OpenReadStream(ContentReader.MaxFileSize);
                var import = await ContentReader.ReadAsync(stream);

                // Title and date come from the export, rounds are started one by one in the live dashboard
                var date = import.Date ?? DateOnly.FromDateTime(DateTime.Today);
                var title = string.IsNullOrWhiteSpace(import.Title)
                    ? GetDefaultTitle(date)
                    : import.Title;

                var created = await LiveQuizService.CreateQuizAsync(
                    title: title,
                    date: date,
                    description: null,
                    content: import.ToContent());

                ToastService.ShowSuccess(
                    $"Quiz night '{created.Title}' created with {import.Rounds.Count} rounds of questions.");

                await OnDataChanged.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isSubmitting = false;
                inputVersion++;
            }
        }

        private async Task CreateQuizNightAsync()
        {
            if (isSubmitting) return;

            isSubmitting = true;
            try
            {
                // Defaults only, everything is edited in the live dashboard afterwards
                var today = DateOnly.FromDateTime(DateTime.Today);
                var created = await LiveQuizService.CreateQuizAsync(
                    title: GetDefaultTitle(today),
                    date: today,
                    description: null);
                ToastService.ShowSuccess($"Quiz night '{created.Title}' created.");

                await OnDataChanged.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isSubmitting = false;
            }
        }

        private async Task HandleDeleteConfirmedAsync()
        {
            if (quizToDelete == null) return;

            try
            {
                await LiveQuizService.DeleteQuizAsync(quizToDelete.Id);
                ToastService.ShowSuccess($"Quiz night '{quizToDelete.Title}' deleted.");

                showDeleteModal = false;
                quizToDelete = null;
                await OnDataChanged.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to delete quiz night: {ex.Message}");
            }
        }

        private async Task HandleReopenConfirmedAsync()
        {
            if (quizToReopen == null) return;

            var title = quizToReopen.Title;
            showReopenModal = false;

            try
            {
                await LiveQuizService.ReopenQuizAsync(quizToReopen.Id);

                ToastService.ShowSuccess($"Quiz night '{title}' reopened.");
                quizToReopen = null;
                await OnQuizReopened.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to reopen quiz night: {ex.Message}");
            }
        }

        private void PromptDelete(Quiz quiz)
        {
            quizToDelete = quiz;
            showDeleteModal = true;
        }

        private void PromptReopen(Quiz quiz)
        {
            quizToReopen = quiz;
            showReopenModal = true;
        }

        #endregion Private Methods
    }
}
