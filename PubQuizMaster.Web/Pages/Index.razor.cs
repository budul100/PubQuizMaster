using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Web.Pages
{
    /// <summary>
    /// Start page: overview of all quiz nights. The live dashboard lives on /quiz/{id}.
    /// </summary>
    public partial class Index
    {
        #region Private Fields

        private List<Quiz> allQuizzes = [];
        private bool isLoading = true;

        #endregion Private Fields

        #region Public Methods

        public void Dispose()
        {
            SessionService.OnRoundChanged -= HandleRoundChanged;
            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            await LoadAsync();

            if (!RendererInfo.IsInteractive) return;

            // Activation, completion and round changes elsewhere move the live badge and the counts
            SessionService.OnRoundChanged += HandleRoundChanged;
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task HandleQuizDeletedAsync()
        {
            // A deleted live night takes its open round with it, scorers and header must follow
            SessionService.NotifyRoundChanged();
            await LoadAsync();
        }

        private void HandleRoundChanged()
        {
            _ = InvokeAsync(async () =>
            {
                await LoadAsync();
                StateHasChanged();
            });
        }

        private async Task LoadAsync()
        {
            allQuizzes = await QuizService.GetAllQuizzesAsync();
            isLoading = false;
        }

        #endregion Private Methods
    }
}
