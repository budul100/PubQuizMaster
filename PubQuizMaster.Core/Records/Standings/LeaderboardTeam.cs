namespace PubQuizMaster.Core.Records.Standings
{
    public record LeaderboardTeam(
        Guid TeamId,
        string TeamName,
        decimal TotalScore,
        int QuizzesPlayed)
    {
        /// <summary>Average points per quiz night played.</summary>
        public decimal AverageScore => QuizzesPlayed > 0 ? TotalScore / QuizzesPlayed : 0m;
    }
}