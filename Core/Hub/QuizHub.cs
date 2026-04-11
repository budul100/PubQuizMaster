using Microsoft.AspNetCore.SignalR;
using PubQuizMaster.Core.Hub.Payloads;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Core.Hub
{
    public class QuizHub(QuizNightService quizNightService, ScorerSessionService scorerSessionService)
        : Microsoft.AspNetCore.SignalR.Hub
    {
        #region Public Methods

        public static async Task NotifyRoundFinalized(IHubContext<QuizHub> hubContext, Guid roundId,
            IEnumerable<LeaderboardEntry> leaderboard)
        {
            var payload = new RoundFinalizedPayload
            {
                RoundId = roundId,
                Leaderboard = leaderboard.ToList()
            };

            await hubContext.Clients.All.SendAsync(
                method: "RoundFinalized",
                arg1: payload);
        }

        public static async Task NotifyRoundStarted(IHubContext<QuizHub> hubContext, Round round,
            IEnumerable<Team> allTeams)
        {
            var payload = new RoundStartedPayload
            {
                Round = RoundSummary.From(round),
                AllTeams = allTeams.ToList()
            };

            await hubContext.Clients.All.SendAsync(
                method: "RoundStarted",
                arg1: payload);
        }

        public override async Task OnConnectedAsync()
        {
            try
            {
                var scorerId = GetScorerIdFromContext();
                System.Diagnostics.Debug.WriteLine($"[Hub] scorerId = '{scorerId}'");
                if (scorerId == null) { Context.Abort(); return; }

                scorerSessionService.RegisterConnection(Context.ConnectionId, scorerId);
                System.Diagnostics.Debug.WriteLine("[Hub] RegisterConnection OK");

                await Groups.AddToGroupAsync(Context.ConnectionId, scorerId);
                System.Diagnostics.Debug.WriteLine("[Hub] AddToGroup OK");

                var assignment = quizNightService.GetAssignment(scorerId);
                System.Diagnostics.Debug.WriteLine($"[Hub] Assignment = {assignment?.ScorerId ?? "null"}");

                var round = quizNightService.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == quizNightService.ActiveRoundId);
                System.Diagnostics.Debug.WriteLine($"[Hub] Round = {round?.Name ?? "null"}");

                var payload = new ScorerConnectedPayload
                {
                    ScorerId = scorerId,
                    Assignment = assignment,
                    Round = round != null ? RoundSummary.From(round) : null,
                    Teams = assignment?.TeamIds
                        .Select(id => quizNightService.QuizNight.MasterTeamList
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

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var scorerId = scorerSessionService.GetScorerId(Context.ConnectionId);

            if (scorerId != null)
            {
                scorerSessionService.RemoveConnection(Context.ConnectionId);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, scorerId);

                // Notify Avalonia host that a scorer went offline
                await Clients.Group(HubGroups.Host).SendAsync("OnScorerDisconnected", scorerId);
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task RequestState()
        {
            var scorerId = GetScorerIdFromContext();
            if (scorerId == null) return;

            var assignment = quizNightService.GetAssignment(scorerId);

            var round = quizNightService.QuizNight.Rounds
                .FirstOrDefault(r => r.Id == quizNightService.ActiveRoundId);

            var payload = new ScorerConnectedPayload
            {
                ScorerId = scorerId,
                Assignment = assignment,
                Round = round != null ? RoundSummary.From(round) : null,
                Teams = assignment?.TeamIds
                    .Select(id => quizNightService.QuizNight.MasterTeamList
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
            var scorerId = GetScorerIdFromContext()
                ?? throw new HubException("Not authenticated.");

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
                var answer = quizNightService.RecordAnswerBool(scorerId, teamId, questionIndex, correct);
                await quizNightService.SaveAsync();
                await BroadcastAnswerUpdate(answer, scorerId); // reuse existing method — sends "OnAnswerUpdated"
            }
            catch (Exception ex)
            {
                throw new HubException(ex.Message);
            }
        }

        public async Task SubmitPointAnswer(Guid teamId, int questionIndex, decimal points)
        {
            var scorerId = scorerSessionService.GetScorerId(Context.ConnectionId);
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
                var answer = quizNightService.RecordAnswerPoint(scorerId, teamId, questionIndex, points);
                await quizNightService.SaveAsync();

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

        public async Task UpdateProgress(int currentQuestionIndex)
        {
            var scorerId = scorerSessionService.GetScorerId(Context.ConnectionId);
            if (scorerId == null) return;

            var assignment = quizNightService.GetAssignment(scorerId);
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

        #region Private Methods

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

        private bool ValidateOwnership(string scorerId, Guid teamId)
        {
            var assignment = quizNightService.GetAssignment(scorerId);
            return assignment?.TeamIds.Contains(teamId) == true;
        }

        #endregion Private Methods
    }
}