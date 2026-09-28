using Microsoft.EntityFrameworkCore;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Data;
using PubQuizMaster.Data.Extensions;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Scorer stations of a quiz night. They are set up before the first round, so scorers can
    /// log in, see their preliminary teams and practice. Starting a round syncs the stations
    /// with the round's assignments, see QuizService.StartRoundAsync.
    /// </summary>
    public class StationService(IDbContextFactory<AppDbContext> dbFactory)
    {
        #region Public Methods

        public async Task<ScorerStation> AddStationAsync(Guid quizId, string label, string scorerId,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            await EnsureEditableAsync(db, quizId, ct);

            var station = new ScorerStation
            {
                QuizId = quizId,
                Label = ValidateLabel(label),
                ScorerId = ValidateScorerId(scorerId)
            };

            db.ScorerStations.Add(station);
            await SaveAsync(db, station.ScorerId, ct);

            return station;
        }

        /// <summary>Stations of the quiz night in label order.</summary>
        public async Task<ScorerStation[]> GetStationsAsync(Guid quizId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var stations = await db.ScorerStations
                .AsNoTracking()
                .Where(s => s.QuizId == quizId)
                .ToArrayAsync(ct);

            return [.. stations.OrderBy(s => s.Label, TeamNameComparer.Instance)];
        }

        public async Task RemoveStationAsync(Guid stationId, CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var station = await db.ScorerStations.FirstOrDefaultAsync(s => s.Id == stationId, ct)
                ?? throw new InvalidOperationException("Scorer station not found.");

            await EnsureEditableAsync(db, station.QuizId, ct);
            await EnsureNotInOpenRoundAsync(db, station, ct);

            db.ScorerStations.Remove(station);
            await db.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Changes label and token of a station. A new token is refused while the station
        /// scores the open round, its scorer would lose the assignment.
        /// </summary>
        public async Task<ScorerStation> UpdateStationAsync(Guid stationId, string label, string scorerId,
            CancellationToken ct = default)
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);

            var station = await db.ScorerStations.FirstOrDefaultAsync(s => s.Id == stationId, ct)
                ?? throw new InvalidOperationException("Scorer station not found.");

            await EnsureEditableAsync(db, station.QuizId, ct);

            var newScorerId = ValidateScorerId(scorerId);

            if (!newScorerId.Equals(station.ScorerId, StringComparison.OrdinalIgnoreCase))
            {
                await EnsureNotInOpenRoundAsync(db, station, ct);
            }

            station.Label = ValidateLabel(label);
            station.ScorerId = newScorerId;

            await SaveAsync(db, newScorerId, ct);

            return station;
        }

        #endregion Public Methods

        #region Private Methods

        private static async Task EnsureEditableAsync(AppDbContext db, Guid quizId, CancellationToken ct)
        {
            var status = await db.Quizzes
                .Where(q => q.Id == quizId)
                .Select(q => (QuizStatus?)q.Status)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("Quiz night not found.");

            if (status == QuizStatus.Completed)
            {
                throw new InvalidOperationException("Stations of a completed quiz night cannot be changed.");
            }
        }

        private static async Task EnsureNotInOpenRoundAsync(AppDbContext db, ScorerStation station, CancellationToken ct)
        {
            var isScoring = await db.Rounds.AnyAsync(r =>
                r.QuizId == station.QuizId
                && !r.IsFinalized
                && r.Assignments.Any(a => a.ScorerId == station.ScorerId), ct);

            if (isScoring)
            {
                throw new InvalidOperationException(
                    $"Station '{station.Label}' is scoring the open round. Finalize the round first.");
            }
        }

        private static async Task SaveAsync(AppDbContext db, string scorerId, CancellationToken ct)
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation(Constraints.ScorerStation))
            {
                throw new InvalidOperationException($"Scorer ID '{scorerId}' is already used by another station.", ex);
            }
        }

        private static string ValidateLabel(string label)
        {
            var trimmed = label.Trim();

            return trimmed.Length > 0
                ? trimmed
                : throw new InvalidOperationException("Station label is required.");
        }

        private static string ValidateScorerId(string scorerId)
        {
            var trimmed = scorerId.Trim();

            return trimmed.Length > 0
                ? trimmed
                : throw new InvalidOperationException("Scorer ID is required.");
        }

        #endregion Private Methods
    }
}
