namespace PubQuizMaster.Web.Pages.Event
{
    /// <summary>
    /// Shortcut to the live quiz night, e.g. from the navigation or the header badge.
    /// Falls back to the overview when no night is live.
    /// </summary>
    public partial class Live
    {
        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            var quizId = await QuizService.GetActiveQuizIdAsync();

            Nav.NavigateTo(quizId == null ? "/" : $"/quiz/{quizId}", replace: true);
        }

        #endregion Protected Methods
    }
}
