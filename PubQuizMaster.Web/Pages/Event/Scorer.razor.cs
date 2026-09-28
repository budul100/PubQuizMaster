using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Content;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Standings;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Common;
using PubQuizMaster.Services.Event;
using PubQuizMaster.Web.Enums;
using PubQuizMaster.Web.Helpers;

namespace PubQuizMaster.Web.Pages.Event
{
    public partial class Scorer
        : ComponentBase, IDisposable
    {
        #region Private Fields

        private readonly CancellationTokenSource heartbeatCts = new();

        // Identifies this page among other open pages of the same scorer station
        private readonly Guid instanceId = Guid.NewGuid();
        private readonly Dictionary<(Guid TeamId, int QuestionIndex), bool?> recordedAnswers = [];

        private List<Team> assignedTeams = [];
        private Core.Models.Event.Scorer? assignment;
        private ElementReference containerRef;
        private ScoringPhase currentPhase = ScoringPhase.Connect;
        private int currentQuestionIndex;
        private string currentScorerId = string.Empty;
        private int currentTeamIndex;
        private string? errorMessage;
        private PeriodicTimer? heartbeatTimer;
        private string inputScorerId = string.Empty;
        private bool isLoading;

        // Practice mode: sample round in this page only, nothing is recorded
        private bool isPractice;

        private bool isRegistered;
        private string? newTeamsHint;

        // Station, quiz night and preliminary teams while no round assigns this station
        private StationPreview? preview;

        private int questionCount = 20;
        private Core.Models.Event.Round? round;

        // Imported questions of the open round, null if the round is not linked to any
        private Core.Models.Content.Round? roundContent;

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

        private Question? CurrentQuestion => roundContent?.GetQuestion(currentQuestionIndex);

        private Team? CurrentTeam => assignedTeams.Count > currentTeamIndex
            ? assignedTeams[currentTeamIndex]
            : null;

        /// <summary>
        /// The cell before the current one in scoring order: the previous sheet of the same question,
        /// or the last sheet of the previous question. Null on the very first cell.
        /// </summary>
        private (Team Team, int QuestionIndex, bool? Answer)? PreviousCell
        {
            get
            {
                if (assignedTeams.Count == 0) return null;

                var (teamIndex, questionIndex) = currentTeamIndex > 0
                    ? (currentTeamIndex - 1, currentQuestionIndex)
                    : (assignedTeams.Count - 1, currentQuestionIndex - 1);

                if (questionIndex < 0 || teamIndex >= assignedTeams.Count) return null;

                var team = assignedTeams[teamIndex];
                return (team, questionIndex, GetAnswer(team.Id, questionIndex));
            }
        }

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
                SessionService.Disconnect(currentScorerId, instanceId);
            }

            GC.SuppressFinalize(this);
        }

        #endregion Public Methods

        #region Protected Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (currentPhase == ScoringPhase.Scoring)
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

        private ScoringType MapPhase(ScoringPhase phase) => isPractice ? ScoringType.Practicing : phase switch
        {
            ScoringPhase.SortSheets => ScoringType.Sorting,

            ScoringPhase.Scoring => ScoringType.Scoring,

            ScoringPhase.Overview => ScoringType.Reviewing,

            _ => ScoringType.Idle
        };

        private void ChangeScorerId()
        {
            if (isRegistered)
            {
                SessionService.Disconnect(currentScorerId, instanceId);
            }

            isRegistered = false;
            isPractice = false;
            preview = null;
            round = null;
            roundContent = null;
            assignment = null;
            assignedTeams = [];
            recordedAnswers.Clear();
            currentPhase = ScoringPhase.Connect;
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
                currentPhase = ScoringPhase.Connect;
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
            if (currentPhase == ScoringPhase.Scoring)
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
                        SetPhase(ScoringPhase.Overview);
                        break;
                }
            }
            else if (currentPhase == ScoringPhase.Overview)
            {
                if (e.Key == "Escape" || e.Key.ToLowerInvariant() == "b")
                {
                    SetPhase(ScoringPhase.Scoring);
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
            SetPhase(ScoringPhase.Scoring);
        }

        private async Task LoadAssignmentAsync()
        {
            var state = await LiveQuizService.GetStateAsync(currentScorerId);

            preview = state.Preview;

            if (state.Round == null || state.Assignment == null || state.AssignedTeams.Count == 0)
            {
                // Practice goes on until a real round assigns this station
                if (isPractice) return;

                round = null;
                roundContent = null;
                assignment = null;
                assignedTeams = [];
                newTeamsHint = null;
                recordedAnswers.Clear();
                SetPhase(ScoringPhase.Waiting);
                return;
            }

            if (isPractice)
            {
                ResetPractice();
                ToastService.ShowSuccess($"{state.Round.Name} started, practice ended.");
            }

            var isNewRound = round?.Id != state.Round.Id;
            var previousTeamIds = assignedTeams.Select(t => t.Id).ToHashSet();
            var previousCurrentTeamId = CurrentTeam?.Id;

            round = state.Round;
            roundContent = state.Content;
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

                // A reopened or reloaded page of a finished station starts at the overview,
                // otherwise it would count as still scoring on the dashboard
                var isComplete = assignedTeams.Count * questionCount <= recordedAnswers.Count;

                var phase = state.ExistingAnswers.Count == 0
                    ? ScoringPhase.SortSheets
                    : isComplete
                        ? ScoringPhase.Overview
                        : ScoringPhase.Scoring;

                SetPhase(phase);
            }
            else
            {
                // Teams registered during the round are inserted alphabetically, the scorer needs the neighbor sheet
                var addedTeamNames = assignedTeams
                    .Select((team, index) => (Team: team, Index: index))
                    .Where(x => !previousTeamIds.Contains(x.Team.Id))
                    .Select(x => x.Index == 0
                        ? $"{x.Team.Name} (on top)"
                        : $"{x.Team.Name} (after {assignedTeams[x.Index - 1].Name})")
                    .ToArray();

                if (addedTeamNames.Length > 0)
                {
                    var added = string.Join(", ", addedTeamNames);
                    newTeamsHint = newTeamsHint == null ? added : $"{newTeamsHint}, {added}";
                }

                // Keep the scorer on the sheet in hand, insertions above it shift the index
                if (previousCurrentTeamId is { } currentTeamId)
                {
                    var index = assignedTeams.FindIndex(t => t.Id == currentTeamId);
                    if (index >= 0) currentTeamIndex = index;
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
                SetPhase(ScoringPhase.Overview);
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

            if (isPractice) return;

            try
            {
                await LiveQuizService.RecordAnswerAsync(
                    roundId: roundId,
                    teamId: teamId,
                    questionIndex: qIdx,
                    isCorrect: isCorrect,
                    scorerId: currentScorerId);

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
                scorerId: currentScorerId,
                instanceId: instanceId,
                phase: MapPhase(currentPhase),
                roundId: isPractice ? null : round?.Id,
                questionIndex: currentQuestionIndex,
                teamIndex: currentTeamIndex);
        }

        /// <summary>Drops the practice data without reporting, a real round or the waiting page follows.</summary>
        private void ResetPractice()
        {
            isPractice = false;
            round = null;
            roundContent = null;
            assignedTeams = [];
            recordedAnswers.Clear();
            newTeamsHint = null;
            currentTeamIndex = 0;
            currentQuestionIndex = 0;
        }

        private void SetPhase(ScoringPhase phase)
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
                        // Keeps this page online; an unchanged position does not outrank
                        // a newer page of the same scorer, see ScorerService.GetStatus
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

        private void StartPractice()
        {
            isPractice = true;
            round = PracticeRound.CreateRound();
            roundContent = PracticeRound.Content;
            assignedTeams = PracticeRound.CreateTeams(preview?.Teams);
            questionCount = round.Length;
            recordedAnswers.Clear();
            newTeamsHint = null;
            currentTeamIndex = 0;
            currentQuestionIndex = 0;

            SetPhase(ScoringPhase.SortSheets);
        }

        private void StartScoring()
        {
            currentTeamIndex = 0;
            currentQuestionIndex = 0;
            SetPhase(ScoringPhase.Scoring);
        }

        private void StopPractice()
        {
            ResetPractice();
            SetPhase(ScoringPhase.Waiting);
        }

        #endregion Private Methods
    }
}
