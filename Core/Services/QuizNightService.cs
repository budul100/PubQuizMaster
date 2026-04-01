using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Services
{
    // ─────────────────────────────────────────────
    // QUIZ NIGHT SERVICE
    // Primary business logic. Manages teams, rounds, and assignments.
    // All mutations go through this service — never modify QuizNight directly.
    // ─────────────────────────────────────────────

    public class QuizNightService
    {
        #region Private Fields

        private readonly PersistenceService _persistence;

        private QuizSessionState? _state;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Primary constructor. Call InitializeAsync() before using any other method.
        /// Separating construction from initialization allows the Avalonia startup
        /// dialog to decide between new night / load existing before state is created.
        /// </summary>
        public QuizNightService(PersistenceService persistence)
        {
            _persistence = persistence;
        }

        #endregion Public Constructors

        #region Public Events

        // Add after the ActiveRoundId property
        public event Action<Answer>? AnswerRecorded
        {
            add { if (_state != null) _state.AnswerRecorded += value; }
            remove { if (_state != null) _state.AnswerRecorded -= value; }
        }

        #endregion Public Events

        #region Public Properties

        public Guid ActiveRoundId => _state?.ActiveRoundId
            ?? throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

        public QuizNight QuizNight => _state?.QuizNight
            ?? throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

        #endregion Public Properties

        #region Public Methods

        /// <summary>Creates a new quiz night and sets it as the active session.</summary>
        public static QuizSessionState CreateNew(PersistenceService persistence, string name)
        {
            var night = new QuizNight
            {
                Name = name,
                Date = DateTime.Today
            };
            return new QuizSessionState(night);
        }

        /// <summary>
        /// Adds a new team to the master list.
        /// SheetOrder defaults to next available number.
        /// </summary>
        public Team AddTeam(string name, int? sheetOrder = null)
        {
            var team = new Team
            {
                Name = name,
                SheetOrder = sheetOrder ?? (QuizNight.MasterTeamList.Count > 0
                    ? QuizNight.MasterTeamList.Max(t => t.SheetOrder) + 1
                    : 1)
            };
            QuizNight.MasterTeamList.Add(team);
            return team;
        }

        /// <summary>Adds a team to an existing round (late join).</summary>
        public void AddTeamToRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);

            if (!QuizNight.MasterTeamList.Any(t => t.Id == teamId))
                throw new ArgumentException($"Team '{teamId}' not found in master list.");

            if (!round.ActiveTeamIds.Contains(teamId))
                round.ActiveTeamIds.Add(teamId);
        }

        /// <summary>
        /// Assigns a set of teams exclusively to a scorer in the given round.
        /// Teams must be active in the round and not already assigned.
        /// </summary>
        public ScorerAssignment AssignScorer(Guid roundId, string scorerId, string label, List<Guid> teamIds)
        {
            var round = GetRoundOrThrow(roundId);

            // Validate: all teams active in round
            var inactiveTeams = teamIds.Except(round.ActiveTeamIds).ToList();
            if (inactiveTeams.Any())
                throw new InvalidOperationException(
                    $"Teams not active in this round: {string.Join(", ", inactiveTeams)}");

            // Validate: no team already assigned
            var alreadyAssigned = round.Assignments
                .SelectMany(a => a.TeamIds)
                .Intersect(teamIds)
                .ToList();

            if (alreadyAssigned.Any())
                throw new InvalidOperationException(
                    $"Teams already assigned to another scorer: {string.Join(", ", alreadyAssigned)}");

            // Teams arrive in sheet order
            var ordered = teamIds
                .OrderBy(id => QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == id)?.SheetOrder ?? 0)
                .ToList();

            var assignment = new ScorerAssignment
            {
                ScorerId = scorerId,
                Label = label,
                TeamIds = ordered
            };

            round.Assignments.Add(assignment);
            return assignment;
        }

        /// <summary>
        /// Creates a new round and activates it.
        /// By default all current master teams are added as active.
        /// </summary>
        public Round CreateRound(string name, int questionCount, List<Guid>? activeTeamIds = null)
        {
            var round = new Round
            {
                Name = name,
                QuestionCount = questionCount,
                ActiveTeamIds = activeTeamIds
                    ?? QuizNight.MasterTeamList
                        .OrderBy(t => t.SheetOrder)
                        .Select(t => t.Id)
                        .ToList()
            };

            QuizNight.Rounds.Add(round);
            _state?.SetActiveRound(round.Id);
            return round;
        }

        public void DeleteRound(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            QuizNight.Rounds.Remove(round);
        }

        // ── Rounds ────────────────────────────────
        /// <summary>Marks a round as finalized. No further answers accepted after this.</summary>
        public void FinalizeRound(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            round.IsFinalized = true;
        }

        /// <summary>
        /// Returns a scorer's current assignment in the active round.
        /// Returns null if not assigned.
        /// </summary>
        public ScorerAssignment? GetAssignment(string scorerId)
        {
            return QuizNight
                .GetAssignment(_state.ActiveRoundId, scorerId);
        }

        /// <summary>Returns the full leaderboard across all rounds.</summary>
        public List<LeaderboardEntry> GetLeaderboard() => QuizNight.GetLeaderboard();

        // ── Scoring & Leaderboard ─────────────────
        /// <summary>
        /// Returns a completion summary for a round:
        /// how many answers have been recorded vs. expected total.
        /// </summary>
        public (int Recorded, int Expected, double PercentComplete) GetRoundProgress(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            var expected = round.ActiveTeamIds.Count * round.QuestionCount;
            var recorded = round.Answers.ToList().Count; // snapshot to avoid race
            var pct = expected == 0 ? 0d : (double)recorded / expected * 100;
            return (recorded, expected, Math.Round(pct, 1));
        }

        /// <summary>
        /// Initializes the service with a QuizNight instance.
        /// Call after the user has chosen to create a new night or load an existing one.
        /// </summary>
        public Task InitializeAsync(QuizNight quizNight)
        {
            _state = new QuizSessionState(quizNight);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Records a boolean answer for the active round.
        /// Convenience wrapper over QuizSessionState.RecordAnswer.
        /// </summary>
        public Answer RecordAnswerBool(string scorerId, Guid teamId, int questionIndex, bool correct)
        {
            return _state?.RecordAnswer(
                _state.ActiveRoundId,
                scorerId,
                teamId,
                questionIndex,
                new AnswerBool { Correct = correct });
        }

        // ── Answers ───────────────────────────────
        /// <summary>
        /// Records a point-based answer for the active round.
        /// </summary>
        public Answer RecordAnswerPoint(string scorerId, Guid teamId, int questionIndex, decimal points)
        {
            return _state.RecordAnswer(
                _state.ActiveRoundId,
                scorerId,
                teamId,
                questionIndex,
                new AnswerPoint { Points = points });
        }

        // ── Scorer Assignments ────────────────────
        /// <summary>Removes a scorer assignment from a round.</summary>
        public void RemoveScorer(Guid roundId, string scorerId)
        {
            var round = GetRoundOrThrow(roundId);
            var assignment = round.Assignments.FirstOrDefault(a => a.ScorerId == scorerId);
            if (assignment != null)
                round.Assignments.Remove(assignment);
        }

        /// <summary>Removes a team from an active round (dropout).</summary>
        public void RemoveTeamFromRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);
            round.ActiveTeamIds.Remove(teamId);
        }

        // ── Teams ─────────────────────────────────
        /// <summary>Reorders teams by updating SheetOrder values.</summary>
        public void ReorderTeams(List<Guid> orderedTeamIds)
        {
            for (var i = 0; i < orderedTeamIds.Count; i++)
            {
                var team = QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == orderedTeamIds[i]);
                if (team != null)
                    team.SheetOrder = i + 1;
            }
        }

        // ── Quiz Night ────────────────────────────
        /// <summary>Persists the current state to disk.</summary>
        public Task SaveAsync() => _persistence.SaveAsync(QuizNight);

        public void SetAnswer(Guid roundId, Guid teamId, int questionIndex, bool? isCorrect)
        {
            var round = QuizNight.Rounds.First(r => r.Id == roundId);
            var existing = round.Answers.FirstOrDefault(a =>
                a.TeamId == teamId && a.QuestionIndex == questionIndex);

            if (isCorrect == null)
            {
                if (existing != null) round.Answers.Remove(existing);
                return;
            }

            var value = new AnswerBool { Correct = isCorrect.Value };

            if (existing != null)
                existing.Value = value;
            else
                round.Answers.Add(new Answer
                {
                    TeamId = teamId,
                    QuestionIndex = questionIndex,
                    Value = value,
                    RecordedByScorerId = "host"
                });
        }

        #endregion Public Methods

        // ── Helpers ───────────────────────────────

        #region Private Methods

        private Round GetRoundOrThrow(Guid roundId)
        {
            return QuizNight.Rounds.FirstOrDefault(r => r.Id == roundId)
                   ?? throw new ArgumentException($"Round '{roundId}' not found.");
        }

        #endregion Private Methods
    }
}