using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Web.Helpers;
using PubQuizMaster.Web.Records;

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
        private Round? roundToDelete;
        private bool showCompleteModal;
        private bool showDeleteRoundModal;
        private bool showDeleteTeamModal;
        private bool showEditModal;
        private bool showSetupModal;
        private string? startRoundError;
        private bool suppressOwnNotification;
        private TeamStanding[] teamStandings = [];
        private TeamStanding? teamToDelete;
        private int ticksSinceFullReload;

        #endregion Private Fields

        #region Private Properties

        [Inject] private ILogger<Index> Logger { get; set; } = null!;

        [Inject] private DownloadService PresentationDownloadService { get; set; } = null!;

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

            GC.SuppressFinalize(this);
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
                var registration = await QuizService.AddTeamAsync(activeNight.Id, name);

                if (registration.AddedToOpenRound)
                {
                    NotifyRoundChanged();
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

                await QuizService.CompleteQuizAsync(activeNight.Id);
                NotifyRoundChanged();

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
                // Tie order within a rank, same comparer as the display order below and the matrix
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            // The round column carries its own badge, so the latest round needs a ranking of its own
            var roundRanks = entries
                .Where(x => x.LatestScore.HasValue)
                .Rank(
                    score: x => x.LatestScore!.Value,
                    isNonCompetitive: x => x.IsNonCompetitive)
                .ToDictionary(x => x.Item.TeamId, x => x.Rank);

            // Ranks come from the ranking, the list itself is alphabetical so teams are easy to find
            teamStandings = [.. entries.Rank(x => x.TotalScore, x => x.IsNonCompetitive)
                .Select(r => new TeamStanding(
                    TeamId: r.Item.TeamId,
                    TeamName: r.Item.Name,
                    LatestRoundScore: r.Item.LatestScore,
                    LatestRoundRank: roundRanks.GetValueOrDefault(r.Item.TeamId),
                    TotalScore: r.Item.TotalScore,
                    OverallRank: r.Rank,
                    IsActive: r.Item.IsActive,
                    IsNonCompetitive: r.Item.IsNonCompetitive,
                    CanDelete: r.Item.CanDelete))
                .OrderBy(s => s.TeamName, StringComparer.CurrentCultureIgnoreCase)];
        }

        private async Task ExportRoundPresentationAsync(ExportRequest request)
        {
            if (activeNight == null || isExporting) return;

            var quiz = activeNight;
            isExporting = true;

            try
            {
                await PresentationDownloadService.DownloadAsync(quiz, request.RoundId, request.SourceFile, cts.Token);
            }
            finally
            {
                isExporting = false;
            }
        }

        private async Task FinalizeRoundAsync()
        {
            if (activeRound == null) return;

            // Second barrier next to the disabled button: the button state of another browser
            // can be older than the current scorer positions
            var scorerIds = activeRound.Assignments
                .Select(a => a.ScorerId).ToArray();

            if (SessionService.IsScoringActive(
                roundId: activeRound.Id,
                scorerIds: scorerIds))
            {
                ToastService.ShowError("Scoring is still running. Wait until every scorer station reaches the overview.");
                return;
            }

            isProcessing = true;
            try
            {
                var wasFinal = activeRound.IsFinal;
                var roundName = activeRound.Name;

                await QuizService.FinalizeRoundAsync(activeRound.Id);
                NotifyRoundChanged();

                ToastService.ShowSuccess($"{roundName} finalized.");

                await LoadDashboardStateAsync();

                // A finalized final round usually ends the night, so offer to complete it right away
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

        private async Task HandleActiveQuizDetailsSavedAsync(QuizDetails update)
        {
            try
            {
                await QuizService.UpdateQuizAsync(update);
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
            // Own notifications are followed by an explicit reload, see NotifyRoundChanged
            if (suppressOwnNotification) return;

            _ = ReloadAsync();
        }

        private async Task HandleDeleteRoundConfirmedAsync()
        {
            if (roundToDelete == null) return;

            var roundId = roundToDelete.Id;
            var roundName = roundToDelete.Name;
            showDeleteRoundModal = false;
            roundToDelete = null;

            try
            {
                await QuizService.DeleteRoundAsync(roundId);
                NotifyRoundChanged();
                ToastService.ShowSuccess($"Round '{roundName}' deleted.");
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private async Task HandleDeleteTeamConfirmedAsync()
        {
            if (activeNight == null || teamToDelete == null) return;

            var teamId = teamToDelete.TeamId;
            var teamName = teamToDelete.TeamName;

            showDeleteTeamModal = false;
            teamToDelete = null;

            try
            {
                await QuizService.RemoveTeamAsync(activeNight.Id, teamId);
                NotifyRoundChanged();
                ToastService.ShowSuccess($"Team '{teamName}' removed from quiz night.");
                await LoadDashboardStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private async Task HandleQuizReopenedAsync()
        {
            // Reopening changes the active quiz night for every circuit (scorers, header, other admins)
            NotifyRoundChanged();
            await LoadDashboardStateAsync();
        }

        private void HandleStatusChanged()
        {
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task LoadDashboardStateAsync()
        {
            var fingerprint = await QuizService.GetActiveQuizFingerprintAsync();

            activeNight = await QuizService.GetActiveQuizAsync();
            lastFingerprint = fingerprint;
            ticksSinceFullReload = 0;

            if (activeNight == null)
            {
                allQuizzes = await QuizService.GetAllQuizzesAsync();
            }
            else
            {
                activeRound = activeNight.Rounds.LastOrDefault(r => !r.IsFinalized);
                ComputeTeamStandings();
            }

            isLoading = false;
        }

        /// <summary>
        /// Notifies other circuits (scorers, second admin browser) about a round change.
        /// The event is raised synchronously, so this component's own handler runs inside the call
        /// and is skipped: every caller reloads explicitly afterwards, which keeps the error path intact.
        /// </summary>
        private void NotifyRoundChanged()
        {
            suppressOwnNotification = true;

            try
            {
                SessionService.NotifyRoundChanged();
            }
            finally
            {
                suppressOwnNotification = false;
            }
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
                var fingerprint = await QuizService.GetActiveQuizFingerprintAsync();
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

        private Task PromptDeleteRound(Round round)
        {
            roundToDelete = round;
            showDeleteRoundModal = true;
            return Task.CompletedTask;
        }

        private void PromptDeleteTeam(Guid teamId)
        {
            teamToDelete = teamStandings.FirstOrDefault(s => s.TeamId == teamId);
            showDeleteTeamModal = teamToDelete != null;
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

        private async Task StartRoundConfirmedAsync(RoundRequest request)
        {
            isProcessing = true;
            try
            {
                var newRound = await QuizService.StartRoundAsync(request);
                NotifyRoundChanged();

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

        private async Task UpdateParticipantStatusAsync((Guid TeamId, bool IsActive, bool IsNonCompetitive) status)
        {
            if (activeNight == null) return;
            try
            {
                await QuizService.SetParticipantStatusAsync(activeNight.Id, status.TeamId, status.IsActive, status.IsNonCompetitive);
                NotifyRoundChanged();
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