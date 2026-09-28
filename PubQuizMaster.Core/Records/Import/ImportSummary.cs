namespace PubQuizMaster.Core.Records.Import
{
    /// <summary>
    /// Outcome of a legacy import. QuizzesUpdated counts existing nights whose question or round count changed.
    /// </summary>
    public record ImportSummary(
        int QuizzesCreated,
        int QuizzesUpdated,
        int TeamsCreated,
        int TeamsBackdated,
        int ResultsCreated,
        int ResultsUpdated,
        int ResultsUnchanged,
        string[] Warnings);
}
