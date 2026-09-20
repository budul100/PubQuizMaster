using System.Collections.Concurrent;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Tracks live scorer stations (presence and workflow position) in memory and
    /// notifies listeners about status, answer and round changes.
    /// </summary>
    public class ScorerService
    {
        #region Private Fields

        private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(25);

        // scorerId -> live status
        private readonly ConcurrentDictionary<string, ScorerStatus> statuses = new(StringComparer.OrdinalIgnoreCase);

        #endregion Private Fields

        #region Public Events

        /// <summary>Raised when answers were persisted. Listeners should reload their data.</summary>
        public event Action? OnAnswersChanged;

        /// <summary>Raised when a round was started or finalized.</summary>
        public event Action? OnRoundChanged;

        /// <summary>Raised when presence or position of a scorer changed. No data reload required.</summary>
        public event Action? OnStatusChanged;

        #endregion Public Events

        #region Public Methods

        public void Disconnect(string scorerId)
        {
            if (string.IsNullOrWhiteSpace(scorerId)) return;

            if (statuses.TryRemove(scorerId.Trim(), out _))
            {
                OnStatusChanged?.Invoke();
            }
        }

        public ScorerStatus? GetStatus(string scorerId)
        {
            if (string.IsNullOrWhiteSpace(scorerId)) return null;

            return statuses.TryGetValue(scorerId.Trim(), out var status)
                ? status
                : null;
        }

        public bool IsConnected(string scorerId)
        {
            var status = GetStatus(scorerId);
            return status != null && DateTime.UtcNow - status.LastSeenUtc < OnlineWindow;
        }

        public void NotifyAnswerRecorded() => OnAnswersChanged?.Invoke();

        public void NotifyRoundChanged() => OnRoundChanged?.Invoke();

        /// <summary>
        /// Stores the full live position of a scorer. Also acts as heartbeat.
        /// </summary>
        public void ReportProgress(string scorerId, ScoringType phase, Guid? roundId,
            int questionIndex, int teamIndex)
        {
            if (string.IsNullOrWhiteSpace(scorerId)) return;

            statuses[scorerId.Trim()] = new ScorerStatus(
                LastSeenUtc: DateTime.UtcNow,
                Phase: phase,
                RoundId: roundId,
                QuestionIndex: questionIndex,
                TeamIndex: teamIndex);

            OnStatusChanged?.Invoke();
        }

        #endregion Public Methods
    }
}