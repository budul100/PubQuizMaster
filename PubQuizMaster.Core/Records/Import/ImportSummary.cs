namespace PubQuizMaster.Core.Records.Import
{
    public record ImportSummary(
        int QuizzesCreated,
        int TeamsCreated,
        int ResultsCreated,
        int ResultsUpdated,
        int ResultsUnchanged,
        string[] Warnings);
}
