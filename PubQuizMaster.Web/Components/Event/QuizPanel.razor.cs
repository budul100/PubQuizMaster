using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Import;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Event
{
    /// <summary>
    /// Start page: every quiz night, the live one on top. A row opens the quiz detail page,
    /// where nights are activated, completed and reopened.
    /// </summary>
    public partial class QuizPanel
    {
        #region Private Fields

        private int inputVersion;
        private bool isSubmitting;
        private Quiz? quizToDelete;
        private bool showDeleteModal;

        #endregion Private Fields

        #region Public Properties

        /// <summary>Correct share of the played nights in date order, legacy imports excluded.</summary>
        [Parameter] public QuizDifficulty[] Difficulties { get; set; } = [];

        /// <summary>Raised after a quiz night was deleted. The page reloads its state.</summary>
        [Parameter] public EventCallback OnDataChanged { get; set; }

        [Parameter] public List<Quiz> QuizNights { get; set; } = [];

        #endregion Public Properties

        #region Private Properties

        /// <summary>Average over the completed nights, the live night is compared against it.</summary>
        private decimal? AverageRate => Difficulties
            .Where(d => d.Status == QuizStatus.Completed)
            .Select(d => (decimal?)d.CorrectRate)
            .DefaultIfEmpty()
            .Average();

        private ChartPoint[] DifficultyPoints => [.. Difficulties.Select(d => new ChartPoint(
            Label: d.Date.ToString("dd.MM.yy"),
            Value: d.CorrectRate,
            Tooltip: $"{d.Title} ({d.Date:dd.MM.yyyy}): {d.CorrectRate:0%} correct"
                + (d.Status == QuizStatus.Live ? ", live" : string.Empty),
            IsHighlighted: d.Status == QuizStatus.Live))];

        /// <summary>Live night first, then the given order (newest first). OrderBy is stable.</summary>
        private Quiz[] OrderedQuizNights => [.. QuizNights.OrderByDescending(q => q.Status == QuizStatus.Live)];

        #endregion Private Properties

        #region Private Methods

        private decimal? RateOf(Quiz quiz) => Difficulties
            .FirstOrDefault(d => d.QuizId == quiz.Id)?.CorrectRate;

        private static string GetDefaultTitle(DateOnly date) => $"Pub Quiz ({date:dd.MM.yyyy})";

        private async Task CreateQuizNightAsync()
        {
            if (isSubmitting) return;

            isSubmitting = true;

            try
            {
                // Defaults only, everything is edited on the detail page afterwards
                var today = DateOnly.FromDateTime(DateTime.Today);

                var created = await LiveQuizService.CreateQuizAsync(
                    title: GetDefaultTitle(today),
                    date: today,
                    description: null);

                ToastService.ShowSuccess($"Quiz night '{created.Title}' created.");
                OpenQuiz(created);
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

        private async Task CreateQuizNightFromFileAsync(InputFileChangeEventArgs e)
        {
            if (isSubmitting) return;

            isSubmitting = true;

            try
            {
                await using var stream = e.File.OpenReadStream(ContentReader.MaxFileSize);
                var import = await ContentReader.ReadAsync(stream);

                // Title and date come from the export, rounds are started one by one on the detail page
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

                OpenQuiz(created);
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

        private void OpenQuiz(Quiz quiz) => Nav.NavigateTo($"/quiz/{quiz.Id}");

        private void PromptDelete(Quiz quiz)
        {
            quizToDelete = quiz;
            showDeleteModal = true;
        }

        #endregion Private Methods
    }
}
