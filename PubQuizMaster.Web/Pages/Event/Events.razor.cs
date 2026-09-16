using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Event;
using PubQuizMaster.Web.Services;

namespace PubQuizMaster.Web.Pages.Event
{
    public partial class Events
    {
        #region Private Fields

        private Guid? exportingQuizId;
        private int exportVersion;
        private bool hasActiveQuiz;
        private bool isLoading = true;
        private bool isSubmitting;
        private DateTime newDate = DateTime.Today;
        private string? newDescription;
        private string newTitle = $"Pub Quiz ({DateTime.Today:dd.MM.yyyy})";
        private List<Quiz> quizNights = [];
        private Quiz? quizToDelete;
        private Quiz? quizToEdit;
        private Quiz? quizToReopen;
        private bool showDeleteModal;
        private bool showEditModal;
        private bool showReopenModal;

        #endregion Private Fields

        #region Private Properties

        [Inject] private PresentationDownloadService PresentationDownloadService { get; set; } = null!;

        [Inject] private ScorerService SessionService { get; set; } = null!;

        #endregion Private Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            await LoadDataAsync();
        }

        #endregion Protected Methods

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
                Nav.NavigateTo("/");
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

                // Recreate the file input, otherwise picking the same file again raises no change event
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
                await LoadDataAsync();
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
                await LoadDataAsync();
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

                // Lets open dashboards pick up the reopened quiz night immediately
                SessionService.NotifyRoundChanged();

                ToastService.ShowSuccess($"Quiz night '{title}' reopened.");
                quizToReopen = null;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to reopen quiz: {ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            isLoading = true;
            try
            {
                quizNights = await LiveQuizService.GetAllQuizzesAsync();
                hasActiveQuiz = quizNights.Any(q => !q.IsCompleted && !q.IsLegacyImport);
            }
            finally
            {
                isLoading = false;
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