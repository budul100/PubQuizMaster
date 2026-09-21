using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Web.Components.Event
{
    /// <summary>
    /// Edits a copy of the quiz details. The passed quiz stays untouched until the parent has saved and reloaded.
    /// </summary>
    public partial class QuizModal
    {
        #region Private Fields

        private DateTime editDate = DateTime.Today;
        private string? editDescription;
        private string editTitle = string.Empty;
        private string? errorMessage;

        // Quiz the edit fields were filled from; parent re-renders must not reset the user's input
        private Guid? initializedQuizId;

        private bool isSaving;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public bool IsOpen { get; set; }

        [Parameter] public EventCallback OnCanceled { get; set; }

        [Parameter] public EventCallback<QuizDetails> OnSaved { get; set; }

        [Parameter] public Quiz? Quiz { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override void OnParametersSet()
        {
            if (!IsOpen || Quiz == null)
            {
                initializedQuizId = null;
                return;
            }

            if (initializedQuizId == Quiz.Id) return;

            editTitle = Quiz.Title;
            editDate = Quiz.Date.ToDateTime(TimeOnly.MinValue);
            editDescription = Quiz.Description;
            errorMessage = null;
            initializedQuizId = Quiz.Id;
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task Cancel() => await OnCanceled.InvokeAsync();

        private async Task SaveAsync()
        {
            if (Quiz == null || isSaving) return;

            if (string.IsNullOrWhiteSpace(editTitle))
            {
                errorMessage = "Title is required.";
                return;
            }

            errorMessage = null;
            isSaving = true;

            try
            {
                var description = !string.IsNullOrWhiteSpace(editDescription)
                    ? editDescription.Trim()
                    : null;

                var update = new QuizDetails(
                    QuizId: Quiz.Id,
                    Title: editTitle.Trim(),
                    Date: DateOnly.FromDateTime(editDate),
                    Description: description);

                // The parent saves, reports errors and closes the modal on success
                await OnSaved.InvokeAsync(update);
            }
            finally
            {
                isSaving = false;
            }
        }

        #endregion Private Methods
    }
}