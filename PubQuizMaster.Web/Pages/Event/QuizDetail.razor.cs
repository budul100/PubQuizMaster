using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Web.Helpers;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Pages.Event
{
    /// <summary>
    /// Detail page of one quiz night, whatever its status. Planned: teams and stations are prepared,
    /// the night is activated here. Live: the dashboard of the running night. Completed: read-only
    /// with matrix and export, the night can be reopened here. Legacy imports show their results.
    /// </summary>
    public partial class QuizDetail
    {
        #region Private Fields

        private const int FullReloadEveryTicks = 15;

        private readonly CancellationTokenSource cts = new();
        private Round? activeRound;
        private string finalizeWarningMessage = string.Empty;
        private bool isExporting;
        private bool isLoading = true;
        private bool isProcessing;
        private bool isReloading;
        private QuizFingerprint? lastFingerprint;
        private Guid loadedQuizId;
        private PeriodicTimer? pollTimer;
        private Quiz? quiz;
        private Round? roundToDelete;
        private bool showCompleteModal;
        private bool showDeleteRoundModal;
        private bool showDeleteTeamModal;
        private bool showEditModal;
        private bool showFinalizeWarningModal;
        private bool showReopenModal;
        private bool showSetupModal;
        private string? startRoundError;
        private bool suppressOwnNotification;
        private TeamStanding[] teamStandings = [];
        private TeamStanding? teamToDelete;
        private int ticksSinceFullReload;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Guid QuizId { get; set; }

        #endregion Public Properties

        #region Private Properties

        private bool IsCompleted => quiz?.Status == QuizStatus.Completed;

        private Result[] LegacyResults => quiz?.Results
            .OrderByDescending(r => r.TotalScore)
            .ThenBy(r => r.Team.Name, TeamNameComparer.Instance)
            .ToArray() ?? [];

        [Inject] private ILogger<QuizDetail> Logger { get; set; } = null!;

        private string PageHeading => quiz?.Status switch
        {
            QuizStatus.Live => "Live",
            QuizStatus.Planned => "Planned",
            _ => "Quiz"
        };

        [Inject] private DownloadService PresentationDownloadService { get; set; } = null!;

        private string StatusBadgeClass => quiz switch
        {
            { IsLegacyImport: true } => "bg-secondary-subtle text-secondary-emphasis",
            { Status: QuizStatus.Live } => "bg-success",
            { Status: QuizStatus.Planned } => "bg-info-subtle text-info-emphasis",
            _ => "bg-secondary"
        };

        private string StatusText => quiz switch
        {
            { IsLegacyImport: true } => "Import",
            { Status: QuizStatus.Live } => "Live",
            { Status: QuizStatus.Planned } => "Planned",
            _ => "Completed"
        };

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

        protected override void OnInitialized()
        {
            if (!RendererInfo.IsInteractive) return;

            SessionService.OnStatusChanged += HandleStatusChanged;
            SessionService.OnAnswersChanged += HandleDataChanged;
            SessionService.OnRoundChanged += HandleDataChanged;

            pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            _ = PollProgressLoopAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            // The component is reused when navigating from one quiz night to another
            if (QuizId == loadedQuizId) return;

            isLoading = true;
            CloseAllModals();

            await LoadStateAsync();
        }

        #endregion Protected Methods

        #region Private Methods

        private async Task ActivateAsync()
        {
            if (quiz == null) return;

            isProcessing = true;

            try
            {
                await QuizService.ActivateQuizAsync(quiz.Id);
                NotifyRoundChanged();

                ToastService.ShowSuccess($"Quiz night '{quiz.Title}' is live.");

                await LoadStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
            finally
            {
                isProcessing = false;
            }
        }

        private async Task AddTeamAsync(string name)
        {
            if (quiz == null || string.IsNullOrWhiteSpace(name)) return;

            try
            {
                var registration = await QuizService.AddTeamAsync(quiz.Id, name);

                // Scorers of an open round get the new sheet, waiting scorers an updated preview
                NotifyRoundChanged();

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

                await LoadStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private void CloseAllModals()
        {
            showCompleteModal = false;
            showDeleteRoundModal = false;
            showDeleteTeamModal = false;
            showEditModal = false;
            showFinalizeWarningModal = false;
            showReopenModal = false;
            CloseStartRoundModal();
        }

        private void CloseStartRoundModal()
        {
            showSetupModal = false;
            startRoundError = null;
        }

        private async Task CompleteQuizAsync()
        {
            if (quiz == null) return;

            showCompleteModal = false;
            isProcessing = true;

            try
            {
                var title = quiz.Title;

                await QuizService.CompleteQuizAsync(quiz.Id);
                NotifyRoundChanged();

                ToastService.ShowSuccess($"Quiz night '{title}' completed.");

                await LoadStateAsync();
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
            if (quiz == null)
            {
                teamStandings = [];
                return;
            }

            var targetRound = activeRound ?? quiz.Rounds.LastOrDefault();
            var targetRoundTeamIds = targetRound?.GetTeamIds() ?? Array.Empty<Guid>();

            var allQuizAnswers = quiz.Rounds.SelectMany(r => r.Answers).ToArray();

            var entries = quiz.ParticipatingTeams
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
                .OrderBy(x => x.Name, TeamNameComparer.Instance)
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
                .OrderBy(s => s.TeamName, TeamNameComparer.Instance)];
        }

        private async Task ExportRoundPresentationAsync(ExportRequest request)
        {
            if (quiz == null || isExporting) return;

            var exportQuiz = quiz;
            isExporting = true;

            try
            {
                await PresentationDownloadService.DownloadAsync(exportQuiz, request.RoundId, request.SourceFile, cts.Token);
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
                var roundName = activeRound.Name;

                await QuizService.FinalizeRoundAsync(activeRound.Id);
                NotifyRoundChanged();

                ToastService.ShowSuccess($"{roundName} finalized.");

                await LoadStateAsync();
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
                await LoadStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private async Task HandleDeleteTeamConfirmedAsync()
        {
            if (quiz == null || teamToDelete == null) return;

            var teamId = teamToDelete.TeamId;
            var teamName = teamToDelete.TeamName;

            showDeleteTeamModal = false;
            teamToDelete = null;

            try
            {
                await QuizService.RemoveTeamAsync(quiz.Id, teamId);
                NotifyRoundChanged();
                ToastService.ShowSuccess($"Team '{teamName}' removed from quiz night.");
                await LoadStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        private async Task HandleFinalizeWarningConfirmedAsync()
        {
            showFinalizeWarningModal = false;
            await FinalizeRoundAsync();
        }

        private async Task HandleQuizDetailsSavedAsync(QuizDetails update)
        {
            try
            {
                await QuizService.UpdateQuizAsync(update);

                // Scorers of an open round reload their state and pick up changed questions
                NotifyRoundChanged();

                ToastService.ShowSuccess("Quiz details updated.");
                showEditModal = false;
                await LoadStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Failed to update quiz: {ex.Message}");
            }
        }

        private async Task HandleStationsChangedAsync()
        {
            // Scorer pages of the night reload, a changed or removed token takes effect right away
            NotifyRoundChanged();
            await LoadStateAsync();
        }

        private void HandleStatusChanged()
        {
            _ = InvokeAsync(StateHasChanged);
        }

        private async Task LoadStateAsync()
        {
            var quizId = QuizId;

            var fingerprint = await QuizService.GetQuizFingerprintAsync(quizId);
            var loaded = await QuizService.GetQuizAsync(quizId);

            // A navigation to another quiz night may have happened meanwhile
            if (quizId != QuizId) return;

            quiz = loaded;
            loadedQuizId = quizId;
            lastFingerprint = fingerprint;
            ticksSinceFullReload = 0;

            activeRound = quiz?.Rounds.LastOrDefault(r => !r.IsFinalized);
            ComputeTeamStandings();

            isLoading = false;
        }

        /// <summary>
        /// Notifies other circuits (scorers, overview, header, second admin browser) about a change.
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
            if (isReloading || isLoading) return;

            ticksSinceFullReload++;

            try
            {
                var fingerprint = await QuizService.GetQuizFingerprintAsync(QuizId);
                if (fingerprint == lastFingerprint && ticksSinceFullReload < FullReloadEveryTicks) return;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Quiz poll failed.");
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
                await LoadStateAsync();
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Quiz reload failed.");
            }
            finally
            {
                isReloading = false;
            }
        });

        private async Task ReopenAsync()
        {
            showReopenModal = false;
            await ActivateAsync();
        }

        /// <summary>
        /// Finalizes the open round right away if it is complete, otherwise asks first.
        /// Incomplete means: answers are missing, or a station is still sorting or scoring.
        /// Checked at click time: the button state of another browser can be outdated.
        /// </summary>
        private async Task RequestFinalizeRoundAsync()
        {
            if (activeRound is not { } round) return;

            var expected = round.GetTeamIds().Length * round.Length;
            var missing = Math.Max(0, expected - round.Answers.Count);
            var pendingScorerIds = round.GetPendingScorerIds();

            var stationNotes = round.Assignments
                .Select(a => new
                {
                    Label = string.IsNullOrWhiteSpace(a.Label) ? a.ScorerId : a.Label,
                    IsPending = pendingScorerIds.Contains(a.ScorerId),
                    IsScoring = SessionService.IsScoringActive(round.Id, [a.ScorerId]),
                })
                .Where(s => s.IsPending || s.IsScoring)
                .OrderBy(s => s.Label, StringComparer.OrdinalIgnoreCase)
                .Select(s => s switch
                {
                    { IsPending: true, IsScoring: true } => $"{s.Label} (still scoring)",
                    { IsPending: true } => $"{s.Label} (sheets incomplete)",
                    _ => $"{s.Label} (not at the overview yet)",
                })
                .ToArray();

            if (missing == 0 && stationNotes.Length == 0)
            {
                await FinalizeRoundAsync();
                return;
            }

            var message = new List<string>();

            if (missing > 0)
            {
                message.Add($"{missing} of {expected} answers are not recorded yet.");
            }

            if (stationNotes.Length > 0)
            {
                message.Add($"Stations not finished: {string.Join(", ", stationNotes)}.");
                message.Add("Answers recorded there after finalizing are rejected.");
            }

            message.Add($"Finalize \"{round.Name}\" anyway?");

            finalizeWarningMessage = string.Join(" ", message);
            showFinalizeWarningModal = true;
        }

        private async Task StartRoundConfirmedAsync(RoundRequest request)
        {
            isProcessing = true;

            try
            {
                var newRound = await QuizService.StartRoundAsync(request);
                NotifyRoundChanged();

                ToastService.ShowSuccess($"{newRound.Name} started.");
                CloseStartRoundModal();
                await LoadStateAsync();
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
            if (quiz == null) return;

            try
            {
                await QuizService.SetParticipantStatusAsync(quiz.Id, status.TeamId, status.IsActive, status.IsNonCompetitive);
                NotifyRoundChanged();
                await LoadStateAsync();
            }
            catch (Exception ex)
            {
                ToastService.ShowError(ex.Message);
            }
        }

        #endregion Private Methods
    }
}
