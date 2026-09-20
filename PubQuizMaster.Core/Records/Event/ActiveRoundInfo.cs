namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Open round of the active quiz night as shown in the header: round name and its scorer stations.
    /// </summary>
    public record ActiveRoundInfo(
        string RoundName,
        string[] ScorerIds);
}
