using System.Collections.Concurrent;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Services.Event
{
    /// <summary>
    /// Tracks live scorer stations (presence and workflow position) in memory and
    /// notifies listeners about status, answer and round changes.
    /// A station can be open in several scorer pages at once (second tab, reloaded page while the
    /// old circuit is still retained). Each page reports under its own instance id; the station
    /// status is the one of the online instance that changed its position last.
    /// </summary>
    public class ScorerService
    {
        #region Private Fields

        private static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(25);

        // scorerId -> instanceId -> live status of that scorer page
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, ScorerStatus>> statuses =
            new(StringComparer.OrdinalIgnoreCase);

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

        /// <summary>Removes one scorer page. Other pages of the same station keep their status.</summary>
        public void Disconnect(string scorerId, Guid instanceId)
        {
            if (string.IsNullOrWhiteSpace(scorerId)) return;

            if (!statuses.TryGetValue(scorerId.Trim(), out var instances)) return;

            if (instances.TryRemove(instanceId, out _))
            {
                OnStatusChanged?.Invoke();
            }
        }

        /// <summary>
        /// Status of the station: the online page with the latest position change,
        /// or the last seen page if none is online.
        /// </summary>
        public ScorerStatus? GetStatus(string scorerId)
        {
            if (string.IsNullOrWhiteSpace(scorerId)) return null;

            if (!statuses.TryGetValue(scorerId.Trim(), out var instances)) return null;

            var all = instances.Values.ToArray();

            return all.Where(IsOnline).MaxBy(s => s.LastChangedUtc)
                ?? all.MaxBy(s => s.LastSeenUtc);
        }

        public bool IsConnected(string scorerId) => IsOnline(GetStatus(scorerId));

        /// <summary>
        /// True while at least one of the given stations is online and still sorting or scoring
        /// the given round. Reviewing the overview counts as done, so it does not block.
        /// </summary>
        public bool IsScoringActive(Guid roundId, IEnumerable<string> scorerIds)
        {
            return scorerIds.Any(id =>
            {
                var status = GetStatus(id);

                return status != null
                    && status.RoundId == roundId
                    && IsOnline(status)
                    && status.Phase is ScoringType.Sorting or ScoringType.Scoring;
            });
        }

        public void NotifyAnswerRecorded() => OnAnswersChanged?.Invoke();

        public void NotifyRoundChanged() => OnRoundChanged?.Invoke();

        /// <summary>
        /// Stores the full live position of one scorer page. Also acts as heartbeat:
        /// an unchanged position only refreshes LastSeenUtc.
        /// </summary>
        public void ReportProgress(string scorerId, Guid instanceId, ScoringType phase, Guid? roundId,
            int questionIndex, int teamIndex)
        {
            if (string.IsNullOrWhiteSpace(scorerId)) return;

            var now = DateTime.UtcNow;
            var instances = statuses.GetOrAdd(scorerId.Trim(), _ => new ConcurrentDictionary<Guid, ScorerStatus>());

            instances.AddOrUpdate(
                instanceId,
                _ => new ScorerStatus(now, now, phase, roundId, questionIndex, teamIndex),
                (_, previous) =>
                {
                    var isUnchanged = previous.Phase == phase
                        && previous.RoundId == roundId
                        && previous.QuestionIndex == questionIndex
                        && previous.TeamIndex == teamIndex;

                    return new ScorerStatus(
                        LastSeenUtc: now,
                        LastChangedUtc: isUnchanged ? previous.LastChangedUtc : now,
                        Phase: phase,
                        RoundId: roundId,
                        QuestionIndex: questionIndex,
                        TeamIndex: teamIndex);
                });

            OnStatusChanged?.Invoke();
        }

        #endregion Public Methods

        #region Private Methods

        private static bool IsOnline(ScorerStatus? status) => status != null
                                                   && DateTime.UtcNow - status.LastSeenUtc < OnlineWindow;

        #endregion Private Methods
    }
}
