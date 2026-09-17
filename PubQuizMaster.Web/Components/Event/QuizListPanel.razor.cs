using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Web.Services;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class QuizListPanel
    {
        #region Private Fields

        private Guid? exportingQuizId;
        private int exportVersion;
        private bool isSubmitting;
        private DateTime newDate = DateTime.Today;
        private string? newDescription;
        private string newTitle = $"Pub Quiz ({DateTime.Today:dd.MM.yyyy})";
        private Quiz? quizToDelete;
        private Quiz? quizToEdit;
        private Quiz? quizToReopen;
        private bool showDeleteModal;
        private bool showEditModal;
        private bool showReopenModal;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public bool HasActiveQuiz { get; set; }

        [Parameter] public bool IsLoading { get; set; }

        [Parameter] public EventCallback OnDataChanged { get; set; }

        [Parameter] public EventCallback<Guid> OnSelectQuiz { get; set; }

        [Parameter] public List<Quiz> QuizNights { get; set; } = [];

        #endregion Public Properties

        #region Private Methods

        private async Task CreateQuizNightAsync()
        {
            if (string.IsNullOrWhiteSpace(newTitle))
            {
                ToastService.ShowError("Title is required.");
                return;
            }

            isSubmitting = true;
            try
            {
                var created = await LiveQuizService.CreateQuizAsync(
                    newTitle,
                    DateOnly.FromDateTime(newDate),
                    newDescription);

                ToastService.ShowSuccess($"Quiz '{created.Title}' created!");
                newTitle = $"Pub Quiz ({DateTime.Today:dd.MM.yyyy})";
                newDescription = null;
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

        private async Task ExportFinalAsync(Quiz quiz, IBrowserFile sourceFile)
        {
            if (exportingQuizId != null) return;

            exportingQuizId = quiz.Id;
            try
            {
                await PresentationDownloadService.DownloadFinalAsync(quiz.Id, sourceFile);
            }
            finally
            {
                exportingQuizId = null;
                exportVersion++;
            }
        }

        private async Task HandleDeleteConfirmedAsync()
        {
            if (quizToDelete == null) return;

            try
            {
                await LiveQuizService.DeleteQuizAsync(quizToDelete.Id);
                ToastService.ShowSuccess($"Quiz '{quizToDelete.Title}' deleted.");
                showDeleteModal = false;
                quizToDelete = null;
                await OnDataChanged.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to delete quiz: {ex.Message}");
            }
        }

        private async Task HandleQuizSavedAsync(QuizDetailsUpdate update)
        {
            try
            {
                await LiveQuizService.UpdateQuizAsync(update);
                ToastService.ShowSuccess("Quiz details updated.");
                showEditModal = false;
                quizToEdit = null;
                await OnDataChanged.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to update quiz: {ex.Message}");
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
                SessionService.NotifyRoundChanged();
                ToastService.ShowSuccess($"Quiz night '{title}' reopened.");
                quizToReopen = null;
                await OnDataChanged.InvokeAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to reopen quiz: {ex.Message}");
            }
        }

        private void OpenEditModal(Quiz quiz)
        {
            quizToEdit = quiz;
            showEditModal = true;
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