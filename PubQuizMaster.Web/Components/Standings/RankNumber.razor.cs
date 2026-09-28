using Microsoft.AspNetCore.Components;

namespace PubQuizMaster.Web.Components.Standings
{
    /// <summary>
    /// Plain place number for rank columns. Teams out of competition carry the shadow rank
    /// of the ranking extension and are shown faded. A rank of 0 renders a dash.
    /// </summary>
    public partial class RankNumber
    {
        #region Public Properties

        [Parameter] public bool IsNonCompetitive { get; set; }

        [Parameter] public int Rank { get; set; }

        #endregion Public Properties
    }
}
