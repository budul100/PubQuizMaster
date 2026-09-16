namespace PubQuizMaster.Core.Records.Player
{
    public record LeaderboardTeam(
        Guid TeamId,
        string TeamName,
        decimal TotalScore,
        int QuizzesPlayed,
        int Rank);
}
