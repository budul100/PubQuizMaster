using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Services
{
    public class DataService(PersistenceService persistenceService)
    {
        #region Public Properties

        public QuizNight QuizNight { get; private set; } = null!;

        #endregion Public Properties

        #region Public Methods

        public static QuizNight CreateNew(string name) => new()
        {
            Name = name,
            Date = DateTime.Today
        };

        public Team AddTeam(string name, int? sheetOrder = null)
        {
            var team = new Team
            {
                Name = name,
                SheetOrder = sheetOrder ?? GetSheetOrder()
            };

            QuizNight.MasterTeamList.Add(team);

            return team;
        }

        public void AddTeamToRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);

            if (!QuizNight.MasterTeamList.Any(t => t.Id == teamId))
                throw new ArgumentException($"Team '{teamId}' not found in MasterTeamList.");

            if (!round.ActiveTeamIds.Contains(teamId))
                round.ActiveTeamIds.Add(teamId);
        }

        public void AssignScorer(Guid roundId, string scorerId, string label, List<Guid> teamIds)
        {
            var round = GetRoundOrThrow(roundId);

            var existing = round.Assignments.FirstOrDefault(a => a.ScorerId == scorerId);

            if (existing != null)
                round.Assignments.Remove(existing);

            round.Assignments.Add(new ScorerAssignment
            {
                ScorerId = scorerId,
                Label = label,
                TeamIds = teamIds
            });
        }

        public Round CreateRound(string name, int questionCount, IEnumerable<Guid> activeTeamIds)
        {
            var round = new Round
            {
                Name = name,
                QuestionCount = questionCount,
                ActiveTeamIds = activeTeamIds.ToList()
            };

            QuizNight.Rounds.Add(round);

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

        public ScorerAssignment? GetAssignment(Guid roundId, string scorerId)
        {
            return QuizNight.Rounds
                .FirstOrDefault(r => r.Id == roundId)?
                .Assignments
                .FirstOrDefault(a => a.ScorerId == scorerId);
        }

        public IEnumerable<LeaderboardEntry> GetLeaderboards()
            => QuizNight.Rounds.Any()
                ? GetLeaderboardsUpTo(QuizNight.Rounds.Last().Id)
                : [];

        public IEnumerable<LeaderboardEntry> GetLeaderboardsUpTo(Guid upToRoundId)
        {
            var roundsInOrder = QuizNight.Rounds
                .TakeWhile(r => r.Id != upToRoundId)
                .Append(QuizNight.Rounds.First(r => r.Id == upToRoundId))
                .ToList();

            var activeTeamIds = roundsInOrder
                .SelectMany(r => r.ActiveTeamIds)
                .Distinct();

            var sorted = activeTeamIds
                .Select(id => GetLeaderboardEntryForRounds(id, roundsInOrder))
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
            var recorded = round.Answers.ToList().Count;
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

            QuizNight = quizNight;
            return Task.CompletedTask;
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
                if (existing != null)
                    round.Answers.Remove(existing);
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

        private LeaderboardEntry GetLeaderboardEntryForRounds(Guid teamId, IList<Round> rounds) => new()
        {
            Team = QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == teamId)
                ?? throw new InvalidOperationException($"Team '{teamId}' not found in MasterTeamList."),

            TotalScore = rounds.Sum(r => r.GetTeamScore(teamId) ?? 0m),

            ScorePerRound = rounds.Select(r => GetRoundScore(r, teamId)).ToList()
        };

        private Round GetRoundOrThrow(Guid roundId) => QuizNight.Rounds.FirstOrDefault(r => r.Id == roundId)
            ?? throw new ArgumentException($"Round '{roundId}' not found.");

        private int GetSheetOrder() => QuizNight.MasterTeamList.Count > 0
            ? QuizNight.MasterTeamList.Max(t => t.SheetOrder) + 1
            : 1;

        #endregion Private Methods
    }
}