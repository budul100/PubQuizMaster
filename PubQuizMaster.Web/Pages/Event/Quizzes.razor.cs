namespace PubQuizMaster.Web.Pages.Event
{
    public partial class Quizzes
    {
        #region Protected Methods

        protected override void OnInitialized()
        {
            Nav.NavigateTo("/", replace: true);
        }

        #endregion Protected Methods
    }
}