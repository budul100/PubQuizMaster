using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Services
{
    public class QuizNightService(PersistenceService persistenceService)
    {
        #region Private Fields

        private QuizSessionState? state;

        #endregion Private Fields

        #region Public Events

        public event Action<Answer>? AnswerRecorded
        {
            add { if (state != null) state.AnswerRecorded += value; }
            remove { if (state != null) state.AnswerRecorded -= value; }
        }

        #endregion Public Events

        #region Public Properties

        public Guid ActiveRoundId => state?.ActiveRoundId
            ?? throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

        public QuizNight QuizNight => state?.QuizNight
            ?? throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

        #endregion Public Properties

        #region Public Methods

        public static QuizSessionState CreateNew(string name)
        {
            var night = new QuizNight
            {
                Name = name,
                Date = DateTime.Today
            };

            return new QuizSessionState(night);
        }

        public Team AddTeam(string name, int? sheetOrder = null)
        {
            var team = new Team
            {
                Name = name,
                SheetOrder = sheetOrder ?? (GetSheetOrder())
            };

            QuizNight.MasterTeamList.Add(team);

            return team;
        }

        public void AddTeamToRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);

            if (!QuizNight.MasterTeamList.Any(t => t.Id == teamId))
            {
                throw new ArgumentException($"Team '{teamId}' not found in master list.");
            }

            if (!round.ActiveTeamIds.Contains(teamId))
            {
                round.ActiveTeamIds.Add(teamId);
            }
        }

        public ScorerAssignment AssignScorer(Guid roundId, string scorerId, string label, List<Guid> teamIds)
        {
            var round = GetRoundOrThrow(roundId);

            // Validate: all teams active in round
            var inactiveTeams = teamIds.Except(round.ActiveTeamIds).ToArray();

            if (inactiveTeams.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Teams not active in this round: {string.Join(", ", inactiveTeams)}");
            }

            // Validate: no team already assigned
            var alreadyAssigned = round.Assignments
                .SelectMany(a => a.TeamIds)
                .Intersect(teamIds).ToArray();

            if (alreadyAssigned.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Teams already assigned to another scorer: {string.Join(", ", alreadyAssigned)}");
            }

            // Teams arrive in sheet order
            var ordered = teamIds
                .OrderBy(id => QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == id)?.SheetOrder ?? 0).ToList();

            var assignment = new ScorerAssignment
            {
                ScorerId = scorerId,
                Label = label,
                TeamIds = ordered
            };

            round.Assignments.Add(assignment);

            return assignment;
        }

        public Round CreateRound(string name, int questionCount, IEnumerable<Guid>? activeTeamIds = null)
        {
            activeTeamIds ??= GetTeamIds();

            var round = new Round
            {
                Name = name,
                QuestionCount = questionCount,
                ActiveTeamIds = activeTeamIds.ToList(),
            };

            QuizNight.Rounds.Add(round);
            state?.SetActiveRound(round.Id);

            return round;
        }

        public void DeleteRound(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            QuizNight.Rounds.Remove(round);
        }

        public void FinalizeRound(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            round.IsFinalized = true;
        }

        public ScorerAssignment? GetAssignment(string scorerId)
        {
            return QuizNight.Rounds
                .FirstOrDefault(r => r.Id == state!.ActiveRoundId)?
                .Assignments
                .FirstOrDefault(a => a.ScorerId == scorerId);
        }

        /// <summary>
        /// Full leaderboard for the entire evening, sorted by total score descending.
        /// Returns one entry per team that was active in at least one round.
        /// </summary>
        public IEnumerable<LeaderboardEntry> GetLeaderboards()
        {
            var activeTeamIds = QuizNight.Rounds
                .SelectMany(r => r.ActiveTeamIds)
                .Distinct();

            var sorted = activeTeamIds
                .Select(GetLeaderboardEntry)
                .Where(e => e.Team != null)
                .OrderByDescending(e => e.TotalScore).ToArray();

            for (var i = 0; i < sorted.Length; i++)
            {
                sorted[i].Rank = i > 0 && sorted[i].TotalScore == sorted[i - 1].TotalScore
                    ? sorted[i - 1].Rank
                    : i + 1;
            }

            return sorted;
        }

        public (int Recorded, int Expected, double PercentComplete) GetRoundProgress(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            var expected = round.ActiveTeamIds.Count * round.QuestionCount;
            var recorded = round.Answers.ToList().Count; // snapshot to avoid race
            var pct = expected == 0 ? 0d : (double)recorded / expected * 100;
            return (recorded, expected, Math.Round(pct, 1));
        }

        public Task InitializeAsync(QuizNight quizNight)
        {
            foreach (var round in quizNight.Rounds)
            {
                if (round.ActiveTeamIds.Count == 0)
                {
                    var teamsWithAnswers = round.Answers
                        .Select(a => a.TeamId)
                        .Distinct().ToList();

                    round.ActiveTeamIds = teamsWithAnswers.Count > 0
                        ? teamsWithAnswers
                        : quizNight.MasterTeamList.Select(t => t.Id).ToList();
                }
            }

            state = new QuizSessionState(quizNight);
            return Task.CompletedTask;
        }

        public Answer RecordAnswerBool(string scorerId, Guid teamId, int questionIndex, bool correct)
        {
            if (state == null)
                throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

            return state.RecordAnswer(
                state.ActiveRoundId,
                scorerId,
                teamId,
                questionIndex,
                new AnswerBool { Correct = correct });
        }

        public Answer RecordAnswerPoint(string scorerId, Guid teamId, int questionIndex, decimal points)
        {
            if (state == null)
                throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

            return state.RecordAnswer(
                state.ActiveRoundId,
                scorerId,
                teamId,
                questionIndex,
                new AnswerPoint { Points = points });
        }

        public void RemoveScorer(Guid roundId, string scorerId)
        {
            var round = GetRoundOrThrow(roundId);
            var assignment = round.Assignments.FirstOrDefault(a => a.ScorerId == scorerId);
            if (assignment != null)
                round.Assignments.Remove(assignment);
        }

        public void RemoveTeamFromRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);
            round.ActiveTeamIds.Remove(teamId);
        }

        public void ReorderTeams(List<Guid> orderedTeamIds)
        {
            for (var i = 0; i < orderedTeamIds.Count; i++)
            {
                var team = QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == orderedTeamIds[i]);
                if (team != null)
                    team.SheetOrder = i + 1;
            }
        }

        public Task SaveAsync() => persistenceService.SaveAsync(QuizNight);

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

        #region Private Methods

        private static RoundScore GetRoundScore(Round round, Guid teamId) => new()
        {
            RoundId = round.Id,
            RoundName = round.Name,
            Score = round.GetTeamScore(teamId)
        };

        private LeaderboardEntry GetLeaderboardEntry(Guid teamId) => new()
        {
            Team = QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == teamId)
                ?? throw new InvalidOperationException($"Team with Id '{teamId}' not found in MasterTeamList."),
            TotalScore = QuizNight.Rounds.Sum(r => r.GetTeamScore(teamId) ?? 0m),
            ScorePerRound = QuizNight.Rounds.Select(r => GetRoundScore(r, teamId)).ToList()
        };

        private Round GetRoundOrThrow(Guid roundId)
        {
            return QuizNight.Rounds.FirstOrDefault(r => r.Id == roundId)
                ?? throw new ArgumentException($"Round '{roundId}' not found.");
        }

        private int GetSheetOrder()
        {
            return QuizNight.MasterTeamList.Count > 0
                ? QuizNight.MasterTeamList.Max(t => t.SheetOrder) + 1
                : 1;
        }

        private Guid[] GetTeamIds()
        {
            return QuizNight.MasterTeamList
                .OrderBy(t => t.SheetOrder)
                .Select(t => t.Id).ToArray();
        }

        #endregion Private Methods
    }
}