using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Event;
using PubQuizMaster.Web.Enums;

namespace PubQuizMaster.Web.Pages.Event
{
    public partial class Scorer
        : ComponentBase, IDisposable
    {
        #region Private Fields

        private readonly CancellationTokenSource heartbeatCts = new();
        private readonly Dictionary<(Guid TeamId, int QuestionIndex), bool?> recordedAnswers = [];

        private List<Team> assignedTeams = [];
        private Core.Models.Event.Scorer? assignment;
        private ElementReference containerRef;
        private ScorerPagePhase currentPhase = ScorerPagePhase.Connect;
        private int currentQuestionIndex;
        private string currentScorerId = string.Empty;
        private int currentTeamIndex;
        private string? errorMessage;
        private PeriodicTimer? heartbeatTimer;
        private string inputScorerId = string.Empty;
        private bool isLoading;
        private bool isRegistered;
        private string? newTeamsHint;
        private int questionCount = 20;
        private Round? round;

        #endregion Private Fields

        #region Public Properties

        [SupplyParameterFromQuery(Name = "scorerId")]
        public string? ScorerIdQuery { get; set; }

        [Parameter]
        public string? ScorerIdRoute { get; set; }

        #endregion Public Properties

        #region Private Properties

        private int CompletionPercentage
        {
            get
            {
                var totalCells = assignedTeams.Count * questionCount;
                if (totalCells == 0) return 0;
                var answered = recordedAnswers.Values.Count(v => v.HasValue);
                return (int)Math.Round((double)answered / totalCells * 100);
            }
        }

        private Team? CurrentTeam => assignedTeams.Count > currentTeamIndex ? assignedTeams[currentTeamIndex] : null;

        [Inject] private QuizService LiveQuizService { get; set; } = null!;

        [Inject] private ScorerService SessionService { get; set; } = null!;

        [Inject] private ToastService ToastService { get; set; } = null!;

        #endregion Private Properties

        #region Public Methods

        public void Dispose()
        {
            SessionService.OnRoundChanged -= HandleRoundChanged;
            heartbeatCts.Cancel();
            heartbeatCts.Dispose();
            heartbeatTimer?.Dispose();

            if (isRegistered)
            {
                SessionService.Disconnect(currentScorerId);
            }
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (currentPhase == ScorerPagePhase.Scoring)
            {
                try
                {
                    await containerRef.FocusAsync();
                }
                catch
                {
                    // Element detached or not yet focusable
                }
            }
        }

        protected override async Task OnInitializedAsync()
        {
            var targetId = !string.IsNullOrWhiteSpace(ScorerIdRoute)
                ? ScorerIdRoute
                : ScorerIdQuery;

            // Prerendering creates a throwaway instance that is disposed right after the HTTP
            // response. Registering the scorer there would be undone by its Dispose call.
            if (!RendererInfo.IsInteractive)
            {
                isLoading = !string.IsNullOrWhiteSpace(targetId);
                return;
            }

            SessionService.OnRoundChanged += HandleRoundChanged;

            if (!string.IsNullOrWhiteSpace(targetId))
            {
                inputScorerId = targetId.Trim();
                await ConnectAsync();
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private static string GetDotClass(bool? val) => val switch
        {
            true => "btn-success",
            false => "btn-danger",
            null => "btn-outline-secondary"
        };

        private static ScoringType MapPhase(ScorerPagePhase phase) => phase switch
        {
            ScorerPagePhase.SortSheets => ScoringType.Sorting,
            ScorerPagePhase.Scoring => ScoringType.Scoring,
            ScorerPagePhase.Overview => ScoringType.Reviewing,
            _ => ScoringType.Idle
        };

        private void ChangeScorerId()
        {
            if (isRegistered)
            {
                SessionService.Disconnect(currentScorerId);
            }

            isRegistered = false;
            round = null;
            assignment = null;
            assignedTeams = [];
            recordedAnswers.Clear();
            currentPhase = ScorerPagePhase.Connect;
        }

        private async Task ConnectAsync()
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(inputScorerId))
            {
                errorMessage = "Please enter a valid Scorer ID.";
                return;
            }

            isLoading = true;
            currentScorerId = inputScorerId.Trim();

            // Register before loading the assignment so the station shows as online
            // even while it waits for the host to start a round.
            isRegistered = true;
            StartHeartbeat();

            try
            {
                await LoadAssignmentAsync();
            }
            catch (Exception ex)
            {
                errorMessage = $"Connection error: {ex.Message}";
                isRegistered = false;
                currentPhase = ScorerPagePhase.Connect;
            }
            finally
            {
                isLoading = false;
            }
        }

        private bool? GetAnswer(Guid teamId, int qIndex)
        {
            return recordedAnswers.TryGetValue((teamId, qIndex), out var val) ? val : null;
        }

        private bool? GetCurrentAnswer()
        {
            if (CurrentTeam == null) return null;
            return GetAnswer(CurrentTeam.Id, currentQuestionIndex);
        }

        private int GetTeamScore(Guid teamId)
        {
            var sum = 0;
            for (var q = 0; q < questionCount; q++)
            {
                if (GetAnswer(teamId, q) == true) sum++;
            }
            return sum;
        }

        private async Task HandleKeyDown(KeyboardEventArgs e)
        {
            if (currentPhase == ScorerPagePhase.Scoring)
            {
                switch (e.Key.ToLowerInvariant())
                {
                    case "y":
                    case "j":
                    case "r":
                    case "1":
                        await RecordAnswerAsync(true);
                        break;

                    case "n":
                    case "f":
                    case "0":
                    case "2":
                        await RecordAnswerAsync(false);
                        break;

                    case "arrowright":
                    case "enter":
                    case "space":
                        NavigateNext();
                        break;

                    case "arrowleft":
                    case "backspace":
                        NavigatePrevious();
                        break;

                    case "o":
                        SetPhase(ScorerPagePhase.Overview);
                        break;
                }
            }
            else if (currentPhase == ScorerPagePhase.Overview)
            {
                if (e.Key == "Escape" || e.Key.ToLowerInvariant() == "b")
                {
                    SetPhase(ScorerPagePhase.Scoring);
                }
            }
        }

        private void HandleRoundChanged()
        {
            if (!isRegistered) return;

            _ = InvokeAsync(async () =>
            {
                try
                {
                    await LoadAssignmentAsync();
                }
                catch (Exception ex)
                {
                    errorMessage = $"Failed to reload assignment: {ex.Message}";
                }

                StateHasChanged();
            });
        }

        private void JumpToCell(int teamIndex, int questionIndex)
        {
            currentTeamIndex = teamIndex;
            currentQuestionIndex = questionIndex;
            SetPhase(ScorerPagePhase.Scoring);
        }

        private async Task LoadAssignmentAsync()
        {
            var state = await LiveQuizService.GetStateAsync(currentScorerId);

            if (state.Round == null || state.Assignment == null || state.AssignedTeams.Count == 0)
            {
                round = null;
                assignment = null;
                assignedTeams = [];
                newTeamsHint = null;
                recordedAnswers.Clear();
                SetPhase(ScorerPagePhase.Waiting);
                return;
            }

            var isNewRound = round?.Id != state.Round.Id;
            var previousTeamIds = assignedTeams.Select(t => t.Id).ToHashSet();

            round = state.Round;
            assignment = state.Assignment;
            assignedTeams = state.AssignedTeams;
            questionCount = round.Length;

            recordedAnswers.Clear();
            foreach (var answer in state.ExistingAnswers)
            {
                recordedAnswers[(answer.TeamId, answer.QuestionIndex)] = answer.Value.GetScore() > 0;
            }

            if (isNewRound)
            {
                newTeamsHint = null;
                currentTeamIndex = 0;
                currentQuestionIndex = 0;
                SetPhase(state.ExistingAnswers.Count == 0 ? ScorerPagePhase.SortSheets : ScorerPagePhase.Scoring);
            }
            else
            {
                // Teams registered during the round are appended, the sheet stack needs to follow
                var addedTeamNames = assignedTeams
                    .Where(t => !previousTeamIds.Contains(t.Id))
                    .Select(t => t.Name)
                    .ToArray();

                if (addedTeamNames.Length > 0)
                {
                    var added = string.Join(", ", addedTeamNames);
                    newTeamsHint = newTeamsHint == null ? added : $"{newTeamsHint}, {added}";
                }

                ReportProgress();
            }
        }

        private void NavigateNext()
        {
            if (currentTeamIndex < assignedTeams.Count - 1)
            {
                currentTeamIndex++;
            }
            else if (currentQuestionIndex < questionCount - 1)
            {
                currentTeamIndex = 0;
                currentQuestionIndex++;
            }
            else
            {
                SetPhase(ScorerPagePhase.Overview);
                return;
            }

            ReportProgress();
        }

        private void NavigatePrevious()
        {
            if (currentTeamIndex > 0)
            {
                currentTeamIndex--;
            }
            else if (currentQuestionIndex > 0)
            {
                currentQuestionIndex--;
                currentTeamIndex = assignedTeams.Count - 1;
            }

            ReportProgress();
        }

        private async Task RecordAnswerAsync(bool isCorrect)
        {
            if (round == null || CurrentTeam == null) return;

            var roundId = round.Id;
            var teamId = CurrentTeam.Id;
            var qIdx = currentQuestionIndex;

            var hadPrevious = recordedAnswers.TryGetValue((teamId, qIdx), out var previous);
            recordedAnswers[(teamId, qIdx)] = isCorrect;

            NavigateNext();

            try
            {
                await LiveQuizService.RecordAnswerAsync(roundId, teamId, qIdx, isCorrect, currentScorerId);

                SessionService.NotifyAnswerRecorded();
            }
            catch (Exception ex)
            {
                // Roll back the optimistic update
                if (hadPrevious)
                {
                    recordedAnswers[(teamId, qIdx)] = previous;
                }
                else
                {
                    recordedAnswers.Remove((teamId, qIdx));
                }
                ToastService.ShowError($"Failed to persist score: {ex.Message}");
            }
        }

        private void ReportProgress()
        {
            if (!isRegistered) return;

            SessionService.ReportProgress(
                currentScorerId,
                MapPhase(currentPhase),
                round?.Id,
                currentQuestionIndex,
                currentTeamIndex);
        }

        private void SetPhase(ScorerPagePhase phase)
        {
            currentPhase = phase;
            ReportProgress();
        }

        private void StartHeartbeat()
        {
            if (heartbeatTimer != null) return;

            var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
            var token = heartbeatCts.Token;
            heartbeatTimer = timer;

            _ = Task.Run(async () =>
            {
                try
                {
                    while (await timer.WaitForNextTickAsync(token))
                    {
                        // Resend the full status so the dashboard can recover it after a Disconnect
                        // triggered by another tab of the same scorer.
                        await InvokeAsync(ReportProgress);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        private void StartScoring()
        {
            currentTeamIndex = 0;
            currentQuestionIndex = 0;
            SetPhase(ScorerPagePhase.Scoring);
        }

        #endregion Private Methods
    }
}