using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Player;
using PubQuizMaster.Core.Scoring;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Pages.Event
{
    public partial class Matrix : IDisposable
    {
        #region Private Fields

        private readonly Dictionary<(Guid TeamId, int QuestionIndex), bool> answersLookup = [];
        private readonly SemaphoreSlim saveLock = new(1, 1);

        private int[] columnSums = [];
        private bool isLoading = true;
        private Dictionary<Guid, decimal> priorScores = [];
        private Round? round;
        private MatrixRow[] rows = [];
        private Team[] teams = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid RoundId { get; set; }

        #endregion Public Properties

        #region Public Methods

        public void Dispose()
        {
            SessionService.OnAnswersChanged -= HandleAnswersChanged;
            saveLock.Dispose();
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            SessionService.OnAnswersChanged += HandleAnswersChanged;
            await LoadMatrixDataAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private static MatrixRow[] AssignRanks(MatrixRow[] source)
        {
            var roundRanks = CompetitionRanking.Rank(source, r => r.RoundScore)
                .ToDictionary(x => x.Item.TeamId, x => x.Rank);

            var overallRanks = CompetitionRanking.Rank(source, r => r.OverallScore)
                .ToDictionary(x => x.Item.TeamId, x => x.Rank);

            return source
                .Select(r => r with
                {
                    RoundRank = roundRanks[r.TeamId],
                    OverallRank = overallRanks[r.TeamId]
                })
                .OrderBy(r => r.TeamName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private void HandleAnswersChanged()
        {
            _ = InvokeAsync(async () =>
            {
                await LoadMatrixDataAsync();
                StateHasChanged();
            });
        }

        private async Task LoadMatrixDataAsync()
        {
            isLoading = true;

            try
            {
                var data = await LiveQuizService.GetMatrixDataAsync(RoundId);

                round = data?.Round;
                teams = data?.Teams ?? [];
                priorScores = data?.PriorScores ?? [];

                answersLookup.Clear();
                if (round != null)
                {
                    foreach (var answer in round.Answers)
                    {
                        answersLookup[(answer.TeamId, answer.QuestionIndex)] = answer.Value.GetScore() > 0;
                    }

                    RebuildMatrixRows();
                }
            }
            catch (Exception ex)
            {
                round = null;
                ToastService.ShowError($"Failed to load matrix: {ex.Message}");
            }
            finally
            {
                isLoading = false;
            }
        }

        private void RebuildMatrixRows()
        {
            if (round == null) return;

            columnSums = new int[round.QuestionCount];
            var unranked = new MatrixRow[teams.Length];

            for (var teamIndex = 0; teamIndex < teams.Length; teamIndex++)
            {
                var team = teams[teamIndex];
                var teamAnswers = new bool[round.QuestionCount];

                for (var q = 0; q < round.QuestionCount; q++)
                {
                    var isCorrect = answersLookup.GetValueOrDefault((team.Id, q), false);
                    teamAnswers[q] = isCorrect;

                    if (isCorrect)
                    {
                        columnSums[q]++;
                    }
                }

                var roundScore = teamAnswers.Count(a => a);
                var overallScore = priorScores.GetValueOrDefault(team.Id) + roundScore;

                unranked[teamIndex] = new MatrixRow(team.Id, team.Name, teamAnswers, roundScore, overallScore);
            }

            rows = AssignRanks(unranked);
        }

        private async Task ToggleAnswerAsync(Guid teamId, int questionIndex)
        {
            if (round == null) return;

            var key = (teamId, questionIndex);
            var previousValue = answersLookup.GetValueOrDefault(key, false);
            var newValue = !previousValue;

            // Optimistic local update
            answersLookup[key] = newValue;
            RebuildMatrixRows();

            await saveLock.WaitAsync();
            try
            {
                var updates = new Dictionary<(Guid TeamId, int QuestionIndex), bool> { [key] = newValue };
                await LiveQuizService.UpdateAnswersAsync(RoundId, updates);
                SessionService.NotifyAnswerRecorded();
            }
            catch (Exception ex)
            {
                // Rollback
                answersLookup[key] = previousValue;
                RebuildMatrixRows();
                ToastService.ShowError($"Failed to save cell: {ex.Message}");
            }
            finally
            {
                saveLock.Release();
            }
        }

        #endregion Private Methods
    }
}