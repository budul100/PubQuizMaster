namespace PubQuizMaster.Core.Records.Import
{
    public record ImportSummary(
        int QuizzesCreated,
        int TeamsCreated,
        int TeamsBackdated,
        int ResultsCreated,
        int ResultsUpdated,
        int ResultsUnchanged,
        string[] Warnings);
}

