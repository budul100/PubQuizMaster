namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// One team row in the round matrix, including round and overall ranking.
    /// </summary>
    public record MatrixRow(
        Guid TeamId,
        string TeamName,
        bool?[] Answers,
        int RoundScore,
        decimal OverallScore,
        int RoundRank = 0,
        int OverallRank = 0);
}