using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using PubQuizMaster.Core.Hub.Payloads;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;
using PubQuizMaster.Core.Services;

// ─────────────────────────────────────────────
// QUIZ HUB
// SignalR hub handling all real-time communication between
// the Avalonia host and connected scorer browser clients.
//
// Clients connect via: /quizhub
// Authentication: scorerId passed on connect via query string
// ─────────────────────────────────────────────

/// <summary>
/// Central SignalR hub for PubQuizMaster.
/// All scorer clients connect here to submit answers and receive live updates.
/// </summary>
public class QuizHub
    : Hub
{
    #region Private Fields

    private readonly QuizNightService _quizNightService;
    private readonly ScorerSessionService _scorerSessionService;

    #endregion Private Fields

    #region Public Constructors

    public QuizHub(QuizNightService quizNightService, ScorerSessionService scorerSessionService)
    {
        _quizNightService = quizNightService;
        _scorerSessionService = scorerSessionService;
    }

    #endregion Public Constructors

    // ── Connection Lifecycle ──────────────────

    #region Public Methods

    /// <summary>
    /// Called from Avalonia host to notify all clients a round has been finalized.
    /// </summary>
    public static async Task NotifyRoundFinalized(
        IHubContext<QuizHub> hubContext,
        Guid roundId,
        List<LeaderboardEntry> leaderboard)
    {
        await hubContext.Clients.All.SendAsync("RoundFinalized", new RoundFinalizedPayload
        {
            RoundId = roundId,
            Leaderboard = leaderboard
        });
    }

    /// <summary>
    /// Called from Avalonia host (via IHubContext) to push a new round to all scorers.
    /// Not invokable by browser clients directly.
    /// </summary>
    public static async Task NotifyRoundStarted(
        IHubContext<QuizHub> hubContext,
        Round round,
        List<Team> allTeams)
    {
        await hubContext.Clients.All.SendAsync("RoundStarted", new RoundStartedPayload
        {
            Round = RoundSummary.From(round),
            AllTeams = allTeams
        });
    }

    /// <summary>
    /// Called when a scorer client connects.
    /// Expects scorerId as a query string parameter: /quizhub?scorerId=abc123
    /// Sends the client its assignment and current round state on connect.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            var scorerId = GetScorerIdFromContext();
            System.Diagnostics.Debug.WriteLine($"[Hub] scorerId = '{scorerId}'");
            if (scorerId == null) { Context.Abort(); return; }

            _scorerSessionService.RegisterConnection(Context.ConnectionId, scorerId);
            System.Diagnostics.Debug.WriteLine("[Hub] RegisterConnection OK");

            await Groups.AddToGroupAsync(Context.ConnectionId, scorerId);
            System.Diagnostics.Debug.WriteLine("[Hub] AddToGroup OK");

            var assignment = _quizNightService.GetAssignment(scorerId);
            System.Diagnostics.Debug.WriteLine($"[Hub] Assignment = {assignment?.ScorerId ?? "null"}");

            var round = _quizNightService.QuizNight.Rounds
                .FirstOrDefault(r => r.Id == _quizNightService.ActiveRoundId);
            System.Diagnostics.Debug.WriteLine($"[Hub] Round = {round?.Name ?? "null"}");

            var payload = new ScorerConnectedPayload
            {
                ScorerId = scorerId,
                Assignment = assignment,
                Round = round != null ? RoundSummary.From(round) : null,
                Teams = assignment?.TeamIds
                    .Select(id => _quizNightService.QuizNight.MasterTeamList
                        .FirstOrDefault(t => t.Id == id))
                    .Where(t => t != null)
                    .ToList(),
                ExistingAnswers = round?.Answers
                    .Where(a => assignment?.TeamIds.Contains(a.TeamId) == true)
                    .ToList()
            };
            System.Diagnostics.Debug.WriteLine("[Hub] Payload built OK");

            await Clients.Caller.SendAsync("ScorerConnected", payload);
            System.Diagnostics.Debug.WriteLine("[Hub] SendAsync OK");

            await base.OnConnectedAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Hub] EXCEPTION: {ex}");
            throw;
        }
    }

    /// <summary>
    /// Called when a scorer client disconnects.
    /// Cleans up connection tracking.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var scorerId = _scorerSessionService.GetScorerId(Context.ConnectionId);

        if (scorerId != null)
        {
            _scorerSessionService.RemoveConnection(Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, scorerId);

            // Notify Avalonia host that a scorer went offline
            await Clients.Group(HubGroups.Host).SendAsync("OnScorerDisconnected", scorerId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    // ── Client → Server: Answer Submission ───

    /// <summary>
    /// Called by scorer clients to submit a boolean answer.
    /// Validates assignment ownership before accepting.
    /// Broadcasts the update to all connected clients on success.
    /// </summary>
    //public async Task SubmitBoolAnswer(Guid teamId, int questionIndex, bool correct)
    //{
    //    var scorerId = _scorerSessionService.GetScorerId(Context.ConnectionId);
    //    if (scorerId == null) return;

    //    if (!ValidateOwnership(scorerId, teamId))
    //    {
    //        await Clients.Caller.SendAsync("OnError", new ErrorPayload
    //        {
    //            Code = "UNAUTHORIZED",
    //            Message = $"Team {teamId} is not assigned to scorer {scorerId}."
    //        });
    //        return;
    //    }

    //    try
    //    {
    //        var answer = _quizNightService.RecordAnswerBool(scorerId, teamId, questionIndex, correct);
    //        await _quizNightService.SaveAsync();

    //        await BroadcastAnswerUpdate(answer, scorerId);
    //    }
    //    catch (Exception ex)
    //    {
    //        await Clients.Caller.SendAsync("OnError", new ErrorPayload
    //        {
    //            Code = "SUBMIT_FAILED",
    //            Message = ex.Message
    //        });
    //    }
    //}

    /// <summary>
    /// Called by scorer clients after reconnect to re-sync state.
    /// Returns the same payload as ScorerConnected.
    /// </summary>
    public async Task RequestState()
    {
        var scorerId = GetScorerIdFromContext();
        if (scorerId == null) return;

        var assignment = _quizNightService.GetAssignment(scorerId);

        var round = _quizNightService.QuizNight.Rounds
            .FirstOrDefault(r => r.Id == _quizNightService.ActiveRoundId);

        var payload = new ScorerConnectedPayload
        {
            ScorerId = scorerId,
            Assignment = assignment,
            Round = round != null ? RoundSummary.From(round) : null,
            Teams = assignment?.TeamIds
                .Select(id => _quizNightService.QuizNight.MasterTeamList
                    .FirstOrDefault(t => t.Id == id))
                .Where(t => t != null)
                .ToList(),
            ExistingAnswers = round?.Answers
                .Where(a => assignment?.TeamIds.Contains(a.TeamId) == true)
                .ToList()
        };

        // Reuse the ScorerConnected event — client already knows how to handle it
        await Clients.Caller.SendAsync("ScorerConnected", payload);
    }

    public async Task SubmitBoolAnswer(Guid roundId, Guid teamId, int questionIndex, bool correct)
    {
        try
        {
            var scorerId = GetScorerIdFromContext() ?? throw new HubException("Not authenticated.");
            var answer = _quizNightService.RecordAnswerBool(scorerId, teamId, questionIndex, correct);

            await Clients.All.SendAsync("AnswerUpdated", new AnswerUpdatedPayload
            {
                TeamId = teamId,
                QuestionIndex = questionIndex,
                Value = new AnswerBool { Correct = correct },
                ScoredBy = scorerId
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Hub] SubmitBoolAnswer EXCEPTION: {ex}");
            throw new HubException(ex.Message);
        }
    }

    /// <summary>
    /// Called by scorer clients to submit a point-based answer.
    /// </summary>
    public async Task SubmitPointAnswer(Guid teamId, int questionIndex, decimal points)
    {
        var scorerId = _scorerSessionService.GetScorerId(Context.ConnectionId);
        if (scorerId == null) return;

        if (!ValidateOwnership(scorerId, teamId))
        {
            await Clients.Caller.SendAsync("OnError", new ErrorPayload
            {
                Code = "UNAUTHORIZED",
                Message = $"Team {teamId} is not assigned to scorer {scorerId}."
            });
            return;
        }

        try
        {
            var answer = _quizNightService.RecordAnswerPoint(scorerId, teamId, questionIndex, points);
            await _quizNightService.SaveAsync();

            await BroadcastAnswerUpdate(answer, scorerId);
        }
        catch (Exception ex)
        {
            await Clients.Caller.SendAsync("OnError", new ErrorPayload
            {
                Code = "SUBMIT_FAILED",
                Message = ex.Message
            });
        }
    }

    // ── Client → Server: Progress Sync ───────

    /// <summary>
    /// Called by a scorer client to update their current question index.
    /// Used to show per-scorer progress in the Avalonia host UI.
    /// </summary>
    public async Task UpdateProgress(int currentQuestionIndex)
    {
        var scorerId = _scorerSessionService.GetScorerId(Context.ConnectionId);
        if (scorerId == null) return;

        var assignment = _quizNightService.GetAssignment(scorerId);
        if (assignment == null) return;

        assignment.CurrentQuestionIndex = currentQuestionIndex;

        // Notify host only — other scorers don't need this
        await Clients.Group(HubGroups.Host).SendAsync("OnScorerProgress", new ScorerProgressPayload
        {
            ScorerId = scorerId,
            Label = assignment.Label,
            CurrentQuestionIndex = currentQuestionIndex
        });
    }

    #endregion Public Methods

    // ── Server → Clients: Host-initiated Events ──
    // ── Helpers ───────────────────────────────

    //private string? GetScorerIdFromContext()
    //{
    //    return Context.GetHttpContext()?.Request.Query["scorerId"].FirstOrDefault();
    //}

    #region Private Methods

    /// <summary>
    /// Broadcasts an answer update to all connected clients.
    /// Used after every successful answer submission.
    /// </summary>
    private async Task BroadcastAnswerUpdate(Answer answer, string scorerId)
    {
        var payload = new AnswerUpdatedPayload
        {
            TeamId = answer.TeamId,
            QuestionIndex = answer.QuestionIndex,
            Value = answer.Value,
            ScoredBy = scorerId,
            Timestamp = answer.RecordedAt
        };

        // Broadcast to everyone: host + all scorer clients
        await Clients.All.SendAsync("OnAnswerUpdated", payload);
    }

    private string? GetScorerIdFromContext()
    {
        var http = Context.GetHttpContext();
        if (http == null)
        {
            Console.WriteLine("[Hub] HttpContext is null");
            return null;
        }

        // Log all query parameters to see what arrives
        foreach (var key in http.Request.Query.Keys)
            Console.WriteLine($"[Hub] Query param: {key} = {http.Request.Query[key]}");

        return http.Request.Query["scorerId"];
    }

    /// <summary>
    /// Validates that the scorer owns the given team in the active round.
    /// </summary>
    private bool ValidateOwnership(string scorerId, Guid teamId)
    {
        var assignment = _quizNightService.GetAssignment(scorerId);
        return assignment?.TeamIds.Contains(teamId) == true;
    }

    #endregion Private Methods
}