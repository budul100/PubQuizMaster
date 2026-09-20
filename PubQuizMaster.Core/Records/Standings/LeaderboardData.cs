namespace PubQuizMaster.Core.Records.Standings
{
    /// <summary>
    /// All-time standings. Rank in the teams is by total score.
    /// Only teams with at least MinQuizzesForAverage nights take part in the ranking by average.
    /// </summary>
    public record LeaderboardData(
        LeaderboardTeam[] Teams,
        int QuizCount,
        int MinQuizzesForAverage);
}
