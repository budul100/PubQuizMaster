namespace PubQuizMaster.Core.Enums
{
    /// <summary>
    /// Lifecycle of a quiz night. Values are stored as integers, do not renumber.
    /// Legacy imports are always Completed.
    /// </summary>
    public enum QuizStatus
    {
        /// <summary>Prepared ahead of the night: teams and scorer stations can be set up, no rounds.</summary>
        Planned = 0,

        /// <summary>The night in progress. At most one quiz night is live at a time.</summary>
        Live = 1,

        /// <summary>Finished, its results count for the all-time standings.</summary>
        Completed = 2,
    }
}
