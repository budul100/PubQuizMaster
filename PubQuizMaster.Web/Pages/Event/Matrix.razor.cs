using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Event;
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
        private bool isRenaming;
        private bool isSavingFinal;
        private bool isSavingName;
        private Dictionary<Guid, decimal> priorScores = [];
        private string renameInput = string.Empty;
        private Round? round;
        private MatrixRow[] rows = [];
        private MatrixTeam[] teams = [];

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid RoundId { get; set; }

        #endregion Public Properties

        #region Public Methods

        public void Dispose()
        {
            SessionService.OnAnswersChanged -= HandleAnswersChanged;
            saveLock.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            SessionService.OnAnswersChanged += HandleAnswersChanged;
            await LoadMatrixDataAsync(showSpinner: true);
        }

        #endregion Protected Methods

        #region Private Methods

        private static MatrixRow[] AssignRanks(MatrixRow[] source)
        {
            var roundRanks = source.Rank(r => r.RoundScore, r => r.IsNonCompetitive)
                .ToDictionary(x => x.Item.TeamId, x => x.Rank);

            var overallRanks = source.Rank(r => r.OverallScore, r => r.IsNonCompetitive)
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

        private void CancelRename()
        {
            isRenaming = false;
            renameInput = string.Empty;
        }

        private void HandleAnswersChanged()
        {
            // Our own save already updated the local state, reloading would only cause flicker
            if (saveLock.CurrentCount == 0) return;

            _ = InvokeAsync(async () =>
            {
                await LoadMatrixDataAsync(showSpinner: false);
                StateHasChanged();
            });
        }

        private async Task HandleRenameKeyDownAsync(KeyboardEventArgs e)
        {
            if (e.Key == "Enter")
            {
                await SaveRenameAsync();
            }
            else if (e.Key == "Escape")
            {
                CancelRename();
            }
        }

        private async Task LoadMatrixDataAsync(bool showSpinner)
        {
            if (showSpinner)
            {
                isLoading = true;
            }

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

            columnSums = new int[round.Length];
            var unranked = new MatrixRow[teams.Length];

            for (var teamIndex = 0; teamIndex < teams.Length; teamIndex++)
            {
                var team = teams[teamIndex];
                var teamAnswers = new bool[round.Length];

                for (var q = 0; q < round.Length; q++)
                {
                    var isCorrect = answersLookup.GetValueOrDefault((team.TeamId, q), false);
                    teamAnswers[q] = isCorrect;

                    if (isCorrect)
                    {
                        columnSums[q]++;
                    }
                }

                var roundScore = teamAnswers.Count(a => a);
                var overallScore = priorScores.GetValueOrDefault(team.TeamId) + roundScore;

                unranked[teamIndex] = new MatrixRow(
                    team.TeamId, team.Name, team.IsNonCompetitive, teamAnswers, roundScore, overallScore);
            }

            rows = AssignRanks(unranked);
        }

        private async Task SaveRenameAsync()
        {
            if (round == null || isSavingName) return;

            if (renameInput.Trim() == round.Name)
            {
                CancelRename();
                return;
            }

            isSavingName = true;

            try
            {
                var savedName = await LiveQuizService.RenameRoundAsync(round.Id, renameInput);
                round.Name = savedName;
                CancelRename();

                // Dashboards and the header badge show the round name
                SessionService.NotifyRoundChanged();
                ToastService.ShowSuccess($"Round renamed to '{savedName}'.");
            }
            catch (Exception ex)
            {
                // Stay in edit mode so the input can be corrected
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isSavingName = false;
            }
        }

        private async Task SetFinalRoundAsync(bool isFinal)
        {
            if (round == null || isSavingFinal) return;

            var previousValue = round.IsFinal;

            // Optimistic local update, the service resets IsFinal on the other rounds of the night
            round.IsFinal = isFinal;
            isSavingFinal = true;

            try
            {
                await LiveQuizService.SetFinalRoundAsync(round.Id, isFinal);
                SessionService.NotifyRoundChanged();

                ToastService.ShowSuccess(isFinal
                    ? $"'{round.Name}' is now the final round."
                    : $"'{round.Name}' is no longer the final round.");
            }
            catch (Exception ex)
            {
                round.IsFinal = previousValue;
                ToastService.ShowError($"Failed to change the final round: {ex.Message}");
            }
            finally
            {
                isSavingFinal = false;
            }
        }

        private void StartRename()
        {
            if (round == null) return;

            renameInput = round.Name;
            isRenaming = true;
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