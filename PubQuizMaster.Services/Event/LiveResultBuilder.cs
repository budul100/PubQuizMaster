using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Scoring;
using PubQuizMaster.Data;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Materializes the totals of completed live quiz nights as Result rows,
    /// so the all-time standings can be summed in SQL.
    /// </summary>
    public static class LiveResultBuilder
    {
        #region Public Methods

        /// <summary>
        /// Replaces the results of the given quiz nights. Only completed live nights get new results,
        /// open live nights lose theirs, legacy nights are ignored.
        /// Call inside a transaction, the old rows are deleted immediately.
        /// </summary>
        public static async Task RebuildAsync(AppDbContext db, Guid[] quizIds, CancellationToken ct)
        {
            if (quizIds.Length == 0) return;

            var liveQuizzes = await db.Quizzes
                .AsNoTracking()
                .Where(q => quizIds.Contains(q.Id) && !q.IsLegacyImport)
                .Select(q => new
                {
                    q.Id,
                    q.IsCompleted,
                    TeamIds = q.ParticipatingTeams.Select(p => p.TeamId).ToArray()
                })
                .ToArrayAsync(ct);

            var liveIds = liveQuizzes.Select(q => q.Id).ToArray();

            await db.Scores
                .Where(s => liveIds.Contains(s.QuizId))
                .ExecuteDeleteAsync(ct);

            var completed = liveQuizzes.Where(q => q.IsCompleted).ToArray();
            if (completed.Length == 0) return;

            var completedIds = completed.Select(q => q.Id).ToArray();

            // Value is jsonb, the score can only be computed in memory
            var answers = await db.Rounds
                .AsNoTracking()
                .Where(r => completedIds.Contains(r.QuizId))
                .SelectMany(r => r.Answers.Select(a => new { r.QuizId, a.TeamId, a.Value }))
                .ToArrayAsync(ct);

            var totals = answers
                .GroupBy(a => (a.QuizId, a.TeamId))
                .ToDictionary(g => g.Key, g => g.Sum(a => a.Value.GetScore()));

            var participants = await db.Participants
                               .AsNoTracking()
                               .Where(p => completedIds.Contains(p.QuizId))
                               .Select(p => new { p.QuizId, p.TeamId, p.IsNonCompetitive })
                               .ToArrayAsync(ct);

            var akLookup = participants.ToDictionary(p => (p.QuizId, p.TeamId), p => p.IsNonCompetitive);

            foreach (var quiz in completed)
            {
                var scores = quiz.TeamIds
                    .Select(teamId => (
                        TeamId: teamId,
                        Score: totals.GetValueOrDefault((quiz.Id, teamId)),
                        IsAK: akLookup.GetValueOrDefault((quiz.Id, teamId), false)))
                    .ToArray();

                foreach (var (entry, rank) in scores.Rank(x => x.Score, x => x.IsAK))
                {
                    db.Scores.Add(new Result
                    {
                        QuizId = quiz.Id,
                        TeamId = entry.TeamId,
                        TotalScore = entry.Score,
                        Rank = rank
                    });
                }
            }

            await db.SaveChangesAsync(ct);
        }

        #endregion Public Methods
    }
}