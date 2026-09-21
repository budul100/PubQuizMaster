namespace PubQuizMaster.Web.Enums
{
    /// <summary>
    /// UI state of the scorer page. Mapped to ScorerPhase for the live status on the dashboard.
    /// </summary>
    public enum ScoringPhase
    {
        Connect,

        Waiting,

        SortSheets,

        Scoring,

        Overview
    }
}
