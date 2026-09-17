using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Core.Scoring;
using PubQuizMaster.Web.Records;
using PubQuizMaster.Web.Services;

namespace PubQuizMaster.Web.Pages
{
    public partial class Index
    {
        #region Private Fields

        private const int FullReloadEveryTicks = 15;

        private readonly CancellationTokenSource cts = new();
        private Quiz? activeNight;
        private Round? activeRound;
        private List<Quiz> allQuizzes = [];
        private bool isExporting;
        private bool isLoading = true;
        private bool isProcessing;
        private bool isReloading;
        private QuizFingerprint? lastFingerprint;
        private PeriodicTimer? pollTimer;
        private bool showCompleteModal;
        private bool showDeleteRoundModal;
        private bool showEditModal;
        private bool showSetupModal;
        private Round? roundToDelete;
        private string? startRoundError;
        private TeamStanding[] teamStandings = [];
        private int ticksSinceFullReload;

        #endregion Private Fields

        #region Private Properties

        [Inject] private ILogger<Index> Logger { get; set; } = null!;

        [Inject] private PresentationDownloadService PresentationDownloadService { get; set; } = null!;

        #endregion Private Properties

        #region Public Methods

        public void Dispose()
        {
            SessionService.OnStatusChanged -= HandleStatusChanged;
            SessionService.OnAnswersChanged -= HandleDataChanged;
            SessionService.OnRoundChanged -= HandleDataChanged;
            cts.Cancel();
            cts.Dispose();
            pollTimer?.Dispose();
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnInitializedAsync()
        {
            await LoadDashboardStateAsync();

            if (!RendererInfo.IsInteractive) return;

            SessionService.OnStatusChanged += HandleStatusChanged;
            SessionService.OnAnswersChanged += HandleDataChanged;
            SessionService.OnRoundChanged += HandleDataChanged;

            pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            _ = PollProgressLoopAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task AddTeamAsync(string name)
        {
            if (activeNight == null || string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var registration = await LiveQuizService.AddTeamAsync(activeNight.Id, name);

                if (registration.AddedToOpenRound)
                {
                    SessionService.NotifyRoundChanged();
                }

                if (!registration.AddedToOpenRound)
                {
                    ToastService.ShowSuccess($"Team '{registration.TeamName}' registered.");
                }
                else if (registration.ScorerLabel != null)
                {
                    ToastService.ShowSuccess(
                        $"Team '{registration.TeamName}' registered and assigned to {registration.ScorerLabel}.");
                }
                else
                {
                    ToastService.ShowError(
                        $"Team '{registration.TeamName}' registered, but the running round has no scorer. " +
                        "Record its answers in the matrix.");
                }

                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private void CloseStartRoundModal()
        {
            showSetupModal = false;
            startRoundError = null;
        }

        private async Task CompleteQuizAsync()
        {
            if (activeNight == null) return;

            showCompleteModal = false;
            isProcessing = true;

            try
            {
                var title = activeNight.Title;

                await LiveQuizService.CompleteQuizAsync(activeNight.Id);
                SessionService.NotifyRoundChanged();

                ToastService.ShowSuccess($"Quiz night '{title}' completed.");

                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to complete quiz night: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }

        private void ComputeTeamStandings()
        {
            if (activeNight == null) return;

            var targetRound = activeRound ?? activeNight.Rounds.LastOrDefault();
            var targetRoundTeamIds = targetRound?.GetTeamIds() ?? Array.Empty<Guid>();

            var allQuizAnswers = activeNight.Rounds.SelectMany(r => r.Answers).ToArray();

            var entries = activeNight.ParticipatingTeams
                .Select(pt => new
                {
                    pt.TeamId,
                    pt.Team.Name,
                    pt.IsActive,
                    pt.IsNonCompetitive,
                    CanDelete = !allQuizAnswers.Any(a => a.TeamId == pt.TeamId),
                    LatestScore = targetRound != null && targetRoundTeamIds.Contains(pt.TeamId)
                        ? targetRound.Answers.Where(a => a.TeamId == pt.TeamId).Sum(a => a.Value.GetScore())
                        : (decimal?)null,
                    TotalScore = allQuizAnswers
                        .Where(a => a.TeamId == pt.TeamId)
                        .Sum(a => a.Value.GetScore())
                })
                .OrderBy(x => x.Name)
                .ToArray();

            teamStandings = [.. CompetitionRanking.Rank(entries, x => x.TotalScore, x => x.IsNonCompetitive)
                .Select(r => new TeamStanding(
                    r.Item.TeamId,
                    r.Item.Name,
                    r.Item.LatestScore,
                    r.Item.TotalScore,
                    r.Rank,
                    r.Item.IsActive,
                    r.Item.IsNonCompetitive,
                    r.Item.CanDelete))];
        }

        private async Task ExportRoundPresentationAsync(RoundExportRequest request)
        {
            if (activeNight == null || isExporting) return;

            var quiz = activeNight;
            isExporting = true;

            try
            {
                await PresentationDownloadService.DownloadAsync(quiz, request.RoundId, request.Mode, request.SourceFile, cts.Token);
            }
            finally
            {
                isExporting = false;
            }
        }

        private async Task FinalizeRoundAsync()
        {
            if (activeRound == null) return;

            isProcessing = true;
            try
            {
                var wasFinal = activeRound.IsFinal;
                var roundName = activeRound.Name;

                await LiveQuizService.FinalizeRoundAsync(activeRound.Id);
                SessionService.NotifyRoundChanged();

                ToastService.ShowSuccess($"{roundName} finalized.");

                await LoadDashboardStateAsync();

                // Z4: Direkt Quiz-Abschluss anbieten, wenn Final-Runde abgeschlossen wurde
                if (wasFinal)
                {
                    showCompleteModal = true;
                }
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to finalize round: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }

        private async Task HandleActiveQuizDetailsSavedAsync(QuizDetailsUpdate update)
        {
            try
            {
                await LiveQuizService.UpdateQuizAsync(update);
                ToastService.ShowSuccess("Quiz details updated.");
                showEditModal = false;
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to update quiz: {ex.Message}");
            }
        }

        private void HandleDataChanged()
        {
            _ = ReloadAsync();
        }

        private void HandleDeleteRoundConfirmedAsync()
        {
            if (roundToDelete == null) return;

            var rId = roundToDelete.Id;
            var rName = roundToDelete.Name;
            showDeleteRoundModal = false;
            roundToDelete = null;

            _ = InvokeAsync(async () =>
            {
                try
                {
                    await LiveQuizService.DeleteRoundAsync(rId);
                    SessionService.NotifyRoundChanged();
                    ToastService.ShowSuccess($"Round '{rName}' deleted.");
                    await LoadDashboardStateAsync();
                }
                catch (Exception ex)
                {
                    ToastService.ShowError(ex.Message);
                }
            });
        }

        private Task HandleSelectQuiz(Guid quizId)
        {
            return LoadDashboardStateAsync();
        }

        private void HandleStatusChanged()
        {
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task LoadDashboardStateAsync()
        {
            var fingerprint = await LiveQuizService.GetActiveQuizFingerprintAsync();

            activeNight = await LiveQuizService.GetActiveQuizAsync();
            lastFingerprint = fingerprint;
            ticksSinceFullReload = 0;

            if (activeNight == null)
            {
                allQuizzes = await LiveQuizService.GetAllQuizzesAsync();
            }
            else
            {
                activeRound = activeNight.Rounds.LastOrDefault(r => !r.IsFinalized);
                ComputeTeamStandings();
            }

            isLoading = false;
        }

        private void OpenStartRoundModal()
        {
            startRoundError = null;
            showSetupModal = true;
        }

        private Task PollAsync() => InvokeAsync(async () =>
        {
            if (isReloading) return;

            ticksSinceFullReload++;

            try
            {
                var fingerprint = await LiveQuizService.GetActiveQuizFingerprintAsync();
                if (fingerprint == lastFingerprint && ticksSinceFullReload < FullReloadEveryTicks) return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Dashboard poll failed.");
                return;
            }

            await ReloadAsync();
        });

        private async Task PollProgressLoopAsync()
        {
            var token = cts.Token;

            try
            {
                while (pollTimer != null && await pollTimer.WaitForNextTickAsync(token))
                {
                    await PollAsync();
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task PromptDeleteTeam(Guid teamId)
        {
            if (activeNight == null) return;
            try
            {
                await LiveQuizService.RemoveTeamAsync(activeNight.Id, teamId);
                SessionService.NotifyRoundChanged();
                ToastService.ShowSuccess("Team removed from quiz night.");
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private Task PromptDeleteRound(Round round)
        {
            roundToDelete = round;
            showDeleteRoundModal = true;
            return Task.CompletedTask;
        }

        private Task ReloadAsync() => InvokeAsync(async () =>
        {
            if (isReloading) return;
            isReloading = true;

            try
            {
                await LoadDashboardStateAsync();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Dashboard reload failed.");
            }
            finally
            {
                isReloading = false;
            }
        });

        private async Task StartRoundConfirmedAsync((RoundRequest Request, bool IsFinal) payload)
        {
            isProcessing = true;
            try
            {
                var newRound = await LiveQuizService.StartRoundAsync(payload.Request, payload.IsFinal);
                SessionService.NotifyRoundChanged();

                ToastService.ShowSuccess($"{newRound.Name} started.");
                CloseStartRoundModal();
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                startRoundError = ex.Message;
            }
            finally
            {
                isProcessing = false;
                StateHasChanged();
            }
        }

        private async Task ToggleFinalRoundAsync((Guid RoundId, bool IsFinal) payload)
        {
            try
            {
                await LiveQuizService.SetFinalRoundAsync(payload.RoundId, payload.IsFinal);
                SessionService.NotifyRoundChanged();
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private async Task UpdateParticipantStatusAsync((Guid TeamId, bool IsActive, bool IsAk) status)
        {
            if (activeNight == null) return;
            try
            {
                await LiveQuizService.SetParticipantStatusAsync(activeNight.Id, status.TeamId, status.IsActive, status.IsAk);
                SessionService.NotifyRoundChanged();
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        #endregion Private Methods
    }
}