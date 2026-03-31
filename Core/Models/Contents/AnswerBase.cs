namespace PubQuizMaster.Core.Models.Contents
{
    // ─────────────────────────────────────────────
    // ANSWER VALUES — extensible without breaking changes
    // ─────────────────────────────────────────────

    /// <summary>
    /// Base class for answer values. Extend for future scoring types.
    /// </summary>
    public abstract class AnswerBase
    {
        /// <summary>Returns the numeric score represented by this answer.</summary>
        public abstract decimal GetScore();
    }
}