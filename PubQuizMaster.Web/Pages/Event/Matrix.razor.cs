using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Player;
using PubQuizMaster.Core.Scoring;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Pages.Event
{
    public partial class Matrix
    {
        #region Private Fields

        private readonly Dictionary<(Guid TeamId, int QuestionIndex), bool?> cellModifications = new();

        private int[] columnSums = [];
        private bool isEditing;
        private bool isLoading = true;
        private bool isSaving;
        private Dictionary<Guid, decimal> priorScores = [];
        private Round? round;
        private MatrixRow[] rows = [];
        private Team[] teams = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid RoundId { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
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

        private void CancelEditing()
        {
            cellModifications.Clear();
            RebuildMatrixRows();
            isEditing = false;
        }

        private void HandleCellClick(Guid teamId, int questionIndex)
        {
            if (!isEditing) return;

            var key = (teamId, questionIndex);
            var current = cellModifications.TryGetValue(key, out var val)
                ? val
                : round?.Answers.FirstOrDefault(a => a.TeamId == teamId && a.QuestionIndex == questionIndex)?.Value.GetScore() > 0;

            // Tri-state cycle: null -> true -> false -> null
            bool? next = current switch
            {
                null => true,
                true => false,
                false => null
            };

            cellModifications[key] = next;
            RebuildMatrixRows();
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

                if (round != null)
                {
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
                var answers = new bool?[round.QuestionCount];

                for (var q = 0; q < round.QuestionCount; q++)
                {
                    if (cellModifications.TryGetValue((team.Id, q), out var modifiedVal))
                    {
                        answers[q] = modifiedVal;
                    }
                    else
                    {
                        var match = round.Answers.FirstOrDefault(a => a.TeamId == team.Id && a.QuestionIndex == q);
                        answers[q] = match == null ? null : match.Value.GetScore() > 0;
                    }

                    if (answers[q] == true)
                    {
                        columnSums[q]++;
                    }
                }

                var roundScore = answers.Count(a => a == true);

                // Overall score across all rounds up to and including this one
                var overallScore = priorScores.GetValueOrDefault(team.Id) + roundScore;

                unranked[teamIndex] = new MatrixRow(team.Id, team.Name, answers, roundScore, overallScore);
            }

            rows = AssignRanks(unranked);
        }

        private async Task SaveMatrixAsync()
        {
            if (cellModifications.Count == 0)
            {
                isEditing = false;
                return;
            }

            isSaving = true;

            try
            {
                await LiveQuizService.UpdateAnswersAsync(RoundId, cellModifications);
                cellModifications.Clear();
                await LoadMatrixDataAsync();

                isEditing = false;
                ToastService.ShowSuccess("Matrix changes saved successfully.");
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to save matrix: {ex.Message}");
            }
            finally
            {
                isSaving = false;
            }
        }

        #endregion Private Methods
    }
}