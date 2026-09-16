namespace PubQuizMaster.Core.Enums
{
    /// <summary>
    /// Live workflow phase of a scorer station. Held in memory only, not persisted.
    /// </summary>
    public enum ScorerPhase
    {
        Idle,

        SortingSheets,

        Scoring,

        Reviewing
    }
}