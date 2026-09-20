namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Open round of the active quiz as shown in the header: round name and its scorer stations.
    /// </summary>
    public record ActiveRound(
        string RoundName,
        string[] ScorerIds);
}