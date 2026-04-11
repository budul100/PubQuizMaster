<!--
INSTRUCTIONS FOR AI:
- This is chunk 1 of 2 of the 'PubQuizMaster' codebase export.
- Do NOT start any analysis, summary, or response until ALL 2 chunks have been provided.
- After each chunk except the last, simply confirm receipt (e.g. "Chunk 1 of 2 received. Please continue.").
- Begin your analysis only after the user explicitly confirms that all chunks have been uploaded.
-->

## FILE: Core\Hub\HubGroups.cs

```cs

// ─────────────────────────────────────────────
// HUB GROUPS
// Constants for SignalR group names.
// ─────────────────────────────────────────────

public static class HubGroups
{
    /// <summary>Group name for the Avalonia host connection (if connected as SignalR client).</summary>
    public const string Host = "host";
}

```


## FILE: Core\Hub\Payloads\AnswerUpdatedPayload.cs

```cs
using PubQuizMaster.Core.Models.Contents;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class AnswerUpdatedPayload
    {
        public Guid TeamId { get; set; }
        public int QuestionIndex { get; set; }
        public AnswerBase Value { get; set; } = default!;
        public string ScoredBy { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
```


## FILE: Core\Hub\Payloads\ErrorPayload.cs

```cs
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PubQuizMaster.Core.Hub.Payloads
{

    public class ErrorPayload
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
```


## FILE: Core\Hub\Payloads\RoundFinalizedPayload.cs

```cs
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundFinalizedPayload
    {
        public Guid RoundId { get; set; }
        public List<LeaderboardEntry> Leaderboard { get; set; } = new();
    }
}
```


## FILE: Core\Hub\Payloads\RoundStartedPayload.cs

```cs
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundStartedPayload
    {
        public RoundSummary Round { get; set; } = new();
        public List<Team> AllTeams { get; set; } = new();
    }
}
```


## FILE: Core\Hub\Payloads\RoundSummary.cs

```cs
using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Hub.Payloads
{
    public class RoundSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public bool IsFinalized { get; set; }

        public static RoundSummary From(Round round) => new()
        {
            Id = round.Id,
            Name = round.Name,
            QuestionCount = round.QuestionCount,
            IsFinalized = round.IsFinalized
        };
    }
}
```


## FILE: Core\Hub\Payloads\ScorerConnectedPayload.cs

```cs
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Hub.Payloads
{
    // ─────────────────────────────────────────────
    // PAYLOADS
    // Strongly typed DTOs for all hub messages.
    // Separate from domain models to keep hub contracts stable
    // even if internal model naming evolves.
    // ─────────────────────────────────────────────

    public class ScorerConnectedPayload
    {
        public string ScorerId { get; set; } = string.Empty;
        public ScorerAssignment? Assignment { get; set; }
        public RoundSummary? Round { get; set; }
        public List<Team?>? Teams { get; set; }
        public List<Answer>? ExistingAnswers { get; set; }
    }
}
```


## FILE: Core\Hub\Payloads\ScorerProgressPayload.cs

```cs
namespace PubQuizMaster.Core.Hub.Payloads
{
    public class ScorerProgressPayload
    {
        public string ScorerId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public int CurrentQuestionIndex { get; set; }
    }
}
```


## FILE: Core\Hub\QuizHub.cs

```cs
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
```


## FILE: Core\Models\ConnectionMode.cs

```cs
namespace PubQuizMaster.Desktop.Models
{
    public enum ConnectionMode 
    { 
        Local, 

        Tunnel 
    }
}

```


## FILE: Core\Models\Contents\Answer.cs

```cs
namespace PubQuizMaster.Core.Models.Contents
{
    /// <summary>
    /// A single recorded answer for one team on one question within a round.
    /// </summary>
    public class Answer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid TeamId { get; set; }
        public int QuestionIndex { get; set; }      // 0-based
        public AnswerBase Value { get; set; } = new AnswerBool();
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public string RecordedByScorerId { get; set; } = string.Empty;
    }
}
```


## FILE: Core\Models\Contents\AnswerBase.cs

```cs
namespace PubQuizMaster.Core.Models.Contents
{
    // ─────────────────────────────────────────────
    // ANSWER VALUES — extensible without breaking changes
    // ─────────────────────────────────────────────

    /// <summary>
    /// Base class for answer values. Extend for future scoring types.
    /// </summary>
    public abstract class AnswerBase
    {
        /// <summary>Returns the numeric score represented by this answer.</summary>
        public abstract decimal GetScore();
    }
}
```


## FILE: Core\Models\Contents\AnswerBool.cs

```cs
namespace PubQuizMaster.Core.Models.Contents
{
    /// <summary>Boolean answer: correct = 1 point, incorrect = 0 points.</summary>
    public class AnswerBool 
        : AnswerBase
    {
        public bool Correct { get; set; }
        public override decimal GetScore() => Correct ? 1m : 0m;
    }
}
```


## FILE: Core\Models\Contents\AnswerPoint.cs

```cs
namespace PubQuizMaster.Core.Models.Contents
{
    /// <summary>Point-based answer for future use (e.g. partial credit).</summary>
    public class AnswerPoint 
        : AnswerBase
    {
        public decimal Points { get; set; }
        public override decimal GetScore() => Points;
    }
}
```


## FILE: Core\Models\Event\QuizNight.cs

```cs
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// The top-level container for an entire quiz evening.
    /// Persisted as a single JSON file.
    /// </summary>
    public class QuizNight
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;       // e.g. "Pub Quiz – 28.03.2026"
        public DateTime Date { get; set; } = DateTime.Today;

        /// <summary>
        /// Master list of all teams that ever participated this evening.
        /// Teams are never deleted — they are deactivated per round.
        /// </summary>
        public List<Team> MasterTeamList { get; set; } = new();

        /// <summary>All rounds in chronological order.</summary>
        public List<Round> Rounds { get; set; } = new();


        // ── Cross-round aggregation ───────────────

        /// <summary>
        /// Total score for one team across all rounds.
        /// Rounds where the team was not active contribute 0 (not null).
        /// </summary>
        public decimal GetTotalScore(Guid teamId)
        {
            return Rounds.Sum(r => r.GetTeamScore(teamId) ?? 0m);
        }

        /// <summary>
        /// Full leaderboard for the entire evening, sorted by total score descending.
        /// Returns one entry per team that was active in at least one round.
        /// </summary>
        public List<LeaderboardEntry> GetLeaderboard()
        {
            var activeTeamIds = Rounds
                .SelectMany(r => r.ActiveTeamIds)
                .Distinct();

            return activeTeamIds
                .Select(teamId => new LeaderboardEntry
                {
                    Team = MasterTeamList.FirstOrDefault(t => t.Id == teamId),
                    TotalScore = GetTotalScore(teamId),
                    ScorePerRound = Rounds
                        .Select(r => new RoundScore
                        {
                            RoundId = r.Id,
                            RoundName = r.Name,
                            Score = r.GetTeamScore(teamId)
                        })
                        .ToList()
                })
                .Where(e => e.Team != null)   // skip orphaned team references
                .OrderByDescending(e => e.TotalScore).ToList();
        }

        /// <summary>
        /// Returns the ScorerAssignment for a given scorer ID in the active round.
        /// Returns null if the scorer is not assigned.
        /// </summary>
        public ScorerAssignment? GetAssignment(Guid roundId, string scorerId)
        {
            return Rounds
                .FirstOrDefault(r => r.Id == roundId)?
                .Assignments
                .FirstOrDefault(a => a.ScorerId == scorerId);
        }
    }
}
```


## FILE: Core\Models\Event\Round.cs

```cs
using PubQuizMaster.Core.Models.Contents;

namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// One quiz round (e.g. "Round 3 – Geography").
    /// Rounds are self-contained: team list, scorer assignments, and answers
    /// all belong to the round. Different rounds can have different team sets.
    /// </summary>
    public class Round
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsFinalized { get; set; } = false;

        /// <summary>
        /// Teams active in this round. May differ from the master team list:
        /// new teams can join, existing teams can drop out.
        /// </summary>
        public List<Guid> ActiveTeamIds { get; set; } = new();

        /// <summary>
        /// Exclusive scorer assignments. Validated on creation:
        /// no team ID may appear in more than one assignment.
        /// </summary>
        public List<ScorerAssignment> Assignments { get; set; } = new();

        /// <summary>All recorded answers for this round.</summary>
        public List<Answer> Answers { get; set; } = new();


        // ── Convenience methods ───────────────────

        /// <summary>
        /// Returns the score for one team in this round.
        /// Teams not active in this round return null (shown as "–", not 0).
        /// </summary>
        public decimal? GetTeamScore(Guid teamId)
        {
            if (!ActiveTeamIds.Contains(teamId)) return null;

            return Answers
                .Where(a => a.TeamId == teamId)
                .Sum(a => a.Value.GetScore());
        }

        /// <summary>
        /// Returns the score for one question across all active teams.
        /// Useful for per-question statistics.
        /// </summary>
        public decimal GetQuestionScore(int questionIndex)
        {
            return Answers
                .Where(a => a.QuestionIndex == questionIndex)
                .Sum(a => a.Value.GetScore());
        }

        /// <summary>
        /// Validates that no team is assigned to more than one scorer.
        /// Call this before saving a new assignment configuration.
        /// </summary>
        public bool ValidateAssignments(out string error)
        {
            var allTeamIds = Assignments.SelectMany(a => a.TeamIds).ToList();
            var duplicates = allTeamIds
                .GroupBy(id => id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicates.Any())
            {
                error = $"Duplicate team assignments: {string.Join(", ", duplicates)}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool? GetAnswer(Guid teamId, int questionIndex)
        {
            var a = Answers.FirstOrDefault(x => x.TeamId == teamId && x.QuestionIndex == questionIndex);
            if (a == null) return null;
            return a.Value.GetScore() > 0;
        }

    }
}
```


## FILE: Core\Models\Event\ScorerAssignment.cs

```cs
namespace PubQuizMaster.Core.Models.Event
{
    /// <summary>
    /// Assigns a set of teams (in scoring-sheet order) exclusively to one scorer.
    /// No team may appear in more than one assignment per round.
    /// </summary>
    public class ScorerAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Stable identifier for the scorer session (e.g. device token).</summary>
        public string ScorerId { get; set; } = string.Empty;

        /// <summary>Human-readable label shown in the UI (e.g. "Scorer B – Tisch 2").</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Ordered list of team IDs. Order must match physical sheet order
        /// so the scorer can work top-to-bottom without looking up names.
        /// </summary>
        public List<Guid> TeamIds { get; set; } = new();

        /// <summary>
        /// The question index this scorer is currently working on.
        /// Independent of other scorers — each scorer has their own pace.
        /// </summary>
        public int CurrentQuestionIndex { get; set; } = 0;
    }
}
```


## FILE: Core\Models\Participants\LeaderboardEntry.cs

```cs
namespace PubQuizMaster.Core.Models.Participants
{
    public class LeaderboardEntry
    {
        public Team Team { get; set; } = new();
        public decimal TotalScore { get; set; }
        public List<RoundScore> ScorePerRound { get; set; } = new();
        public int Rank { get; set; }   // set by caller after sorting
    }
}
```


## FILE: Core\Models\Participants\RoundScore.cs

```cs
namespace PubQuizMaster.Core.Models.Participants
{
    // ─────────────────────────────────────────────
    // LEADERBOARD PROJECTIONS
    // ─────────────────────────────────────────────

    public class RoundScore
    {
        public Guid RoundId { get; set; }
        public string RoundName { get; set; } = string.Empty;

        /// <summary>Null means the team was not active in this round (display as "–").</summary>
        public decimal? Score { get; set; }
    }
}
```


## FILE: Core\Models\Participants\Team.cs

```cs
namespace PubQuizMaster.Core.Models.Participants
{
    // ─────────────────────────────────────────────
    // CORE ENTITIES
    // ─────────────────────────────────────────────

    /// <summary>
    /// A team participating in the quiz night.
    /// Teams persist across all rounds — they are activated per round.
    /// </summary>
    public class Team
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Physical order of the scoring sheets — scorers sort their
        /// answer sheets by this number before starting.
        /// </summary>
        public int SheetOrder { get; set; }
    }
}
```


## FILE: Core\Models\QuizSessionState.cs

```cs
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Core.Models
{


    // ─────────────────────────────────────────────
    // SESSION STATE (runtime only, not persisted)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Thread-safe runtime state shared between the Avalonia UI and the Kestrel/SignalR layer.
    /// The QuizNight object itself is the persistent model; this wraps it with
    /// concurrency handling and change notifications.
    /// </summary>
    public class QuizSessionState
    {
        private readonly object _lock = new();

        public QuizNight QuizNight { get; private set; }
        public Guid ActiveRoundId { get; private set; }

        /// <summary>Fired whenever an answer is recorded — Avalonia UI subscribes to this.</summary>
        public event Action<Answer>? AnswerRecorded;

        public QuizSessionState(QuizNight quizNight)
        {
            QuizNight = quizNight;
            ActiveRoundId = quizNight.Rounds.LastOrDefault()?.Id ?? Guid.Empty;
        }

        /// <summary>
        /// Records or overwrites an answer. Thread-safe.
        /// Raises AnswerRecorded after saving.
        /// </summary>
        public Answer RecordAnswer(Guid roundId, string scorerId, Guid teamId, int questionIndex, AnswerBase value)
        {
            lock (_lock)
            {
                var round = QuizNight.Rounds.First(r => r.Id == roundId);

                // Validate scorer owns this team
                var assignment = round.Assignments.FirstOrDefault(a => a.ScorerId == scorerId);
                if (assignment == null || !assignment.TeamIds.Contains(teamId))
                    throw new InvalidOperationException($"Scorer '{scorerId}' is not assigned to team '{teamId}'.");

                // Overwrite existing answer if present (correction support)
                var existing = round.Answers.FirstOrDefault(a =>
                    a.TeamId == teamId && a.QuestionIndex == questionIndex);

                if (existing != null)
                    round.Answers.Remove(existing);

                var answer = new Answer
                {
                    TeamId = teamId,
                    QuestionIndex = questionIndex,
                    Value = value,
                    RecordedByScorerId = scorerId,
                    RecordedAt = DateTime.UtcNow
                };

                round.Answers.Add(answer);

                // Advance scorer's current position if moving forward
                if (assignment.CurrentQuestionIndex <= questionIndex)
                    assignment.CurrentQuestionIndex = questionIndex;

                AnswerRecorded?.Invoke(answer);
                return answer;
            }
        }

        /// <summary>Switches the active round. Pass an existing or newly created Round.</summary>
        public void SetActiveRound(Guid roundId)
        {
            lock (_lock)
            {
                if (QuizNight.Rounds.All(r => r.Id != roundId))
                    throw new ArgumentException("Round not found.");
                ActiveRoundId = roundId;
            }
        }
    }
}
```


## FILE: Core\Models\Settings.cs

```cs
namespace PubQuizMaster.Desktop.Models
{
    public class Settings
    {
        #region Public Properties

        public string? ClientUrl { get; set; }
        
        public string? PinggyToken { get; set; }

        #endregion Public Properties
    }
}
```


## FILE: Core\PubQuizMaster.Core.csproj

```csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.SignalR" Version="1.2.9" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
  </ItemGroup>

</Project>

```


## FILE: Core\Services\AnswerBaseJsonConverter.cs

```cs
using PubQuizMaster.Core.Models.Contents;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubQuizMaster.Core.Services
{
    /// <summary>
    /// Polymorphic JSON converter for AnswerBase (AnswerBool / AnswerPoint).
    /// Writes a "$type" discriminator so deserialization knows which subclass to use.
    /// </summary>
    public class AnswerBaseJsonConverter : JsonConverter<AnswerBase>
    {
        public override AnswerBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var type = root.GetProperty("$type").GetString();
            return type switch
            {
                nameof(AnswerBool) => JsonSerializer.Deserialize<AnswerBool>(root.GetRawText(), options)!,
                nameof(AnswerPoint) => JsonSerializer.Deserialize<AnswerPoint>(root.GetRawText(), options)!,
                _ => throw new JsonException($"Unknown AnswerBase type: {type}")
            };
        }

        public override void Write(Utf8JsonWriter writer, AnswerBase value, JsonSerializerOptions options)
        {
            var type = value.GetType().Name;
            var json = JsonSerializer.SerializeToElement(value, value.GetType(), options);

            writer.WriteStartObject();
            writer.WriteString("$type", type);
            foreach (var prop in json.EnumerateObject())
                prop.WriteTo(writer);
            writer.WriteEndObject();
        }
    }
}
```


## FILE: Core\Services\PersistenceService.cs

```cs
using PubQuizMaster.Core.Models.Event;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PubQuizMaster.Core.Services
{
    // ─────────────────────────────────────────────
    // PERSISTENCE SERVICE
    // Handles JSON save/load of the QuizNight model.
    // Single file per quiz night, stored in AppData or next to the executable.
    // ─────────────────────────────────────────────

    /// <summary>
    /// Persists and restores QuizNight objects as JSON files.
    /// File naming convention: "quiznight_{date}_{id_short}.json"
    /// </summary>
    public class PersistenceService
    {
        private readonly string _storageDirectory;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters = { new AnswerBaseJsonConverter() }
        };

        public PersistenceService(string? storageDirectory = null)
        {
            _storageDirectory = storageDirectory
                ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "PubQuizMaster");

            Directory.CreateDirectory(_storageDirectory);
        }

        /// <summary>Saves the quiz night to disk. Overwrites if file exists.</summary>
        public async Task SaveAsync(QuizNight quizNight)
        {
            var path = GetFilePath(quizNight);
            var json = JsonSerializer.Serialize(quizNight, _jsonOptions);
            await File.WriteAllTextAsync(path, json);
        }

        /// <summary>Loads a quiz night by its file path.</summary>
        public async Task<QuizNight> LoadAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<QuizNight>(json, _jsonOptions)
                   ?? throw new InvalidDataException($"Failed to deserialize: {filePath}");
        }

        /// <summary>Returns all saved quiz night files, newest first.</summary>
        public IEnumerable<FileInfo> ListSavedNights()
        {
            return new DirectoryInfo(_storageDirectory)
                .GetFiles("quiznight_*.json")
                .OrderByDescending(f => f.LastWriteTime);
        }

        private string GetFilePath(QuizNight quizNight)
        {
            var datePart = quizNight.Date.ToString("yyyy-MM-dd");
            var idPart = quizNight.Id.ToString()[..8];
            return Path.Combine(_storageDirectory, $"quiznight_{datePart}_{idPart}.json");
        }
    }
}
```


## FILE: Core\Services\QuizNightService.cs

```cs
using PubQuizMaster.Core.Models;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Core.Services
{
    // ─────────────────────────────────────────────
    // QUIZ NIGHT SERVICE
    // Primary business logic. Manages teams, rounds, and assignments.
    // All mutations go through this service — never modify QuizNight directly.
    // ─────────────────────────────────────────────

    public class QuizNightService
    {
        #region Private Fields

        private readonly PersistenceService _persistence;

        private QuizSessionState? _state;

        #endregion Private Fields

        #region Public Constructors

        /// <summary>
        /// Primary constructor. Call InitializeAsync() before using any other method.
        /// Separating construction from initialization allows the Avalonia startup
        /// dialog to decide between new night / load existing before state is created.
        /// </summary>
        public QuizNightService(PersistenceService persistence)
        {
            _persistence = persistence;
        }

        #endregion Public Constructors

        #region Public Events

        // Add after the ActiveRoundId property
        public event Action<Answer>? AnswerRecorded
        {
            add { if (_state != null) _state.AnswerRecorded += value; }
            remove { if (_state != null) _state.AnswerRecorded -= value; }
        }

        #endregion Public Events

        #region Public Properties

        public Guid ActiveRoundId => _state?.ActiveRoundId
            ?? throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

        public QuizNight QuizNight => _state?.QuizNight
            ?? throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

        #endregion Public Properties

        #region Public Methods

        public static QuizSessionState CreateNew(PersistenceService persistence, string name)
        {
            var night = new QuizNight
            {
                Name = name,
                Date = DateTime.Today
            };
            return new QuizSessionState(night);
        }

        public Team AddTeam(string name, int? sheetOrder = null)
        {
            var team = new Team
            {
                Name = name,
                SheetOrder = sheetOrder ?? (QuizNight.MasterTeamList.Count > 0
                    ? QuizNight.MasterTeamList.Max(t => t.SheetOrder) + 1
                    : 1)
            };
            QuizNight.MasterTeamList.Add(team);
            return team;
        }

        public void AddTeamToRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);

            if (!QuizNight.MasterTeamList.Any(t => t.Id == teamId))
                throw new ArgumentException($"Team '{teamId}' not found in master list.");

            if (!round.ActiveTeamIds.Contains(teamId))
                round.ActiveTeamIds.Add(teamId);
        }

        public ScorerAssignment AssignScorer(Guid roundId, string scorerId, string label, List<Guid> teamIds)
        {
            var round = GetRoundOrThrow(roundId);

            // Validate: all teams active in round
            var inactiveTeams = teamIds.Except(round.ActiveTeamIds).ToList();
            if (inactiveTeams.Any())
                throw new InvalidOperationException(
                    $"Teams not active in this round: {string.Join(", ", inactiveTeams)}");

            // Validate: no team already assigned
            var alreadyAssigned = round.Assignments
                .SelectMany(a => a.TeamIds)
                .Intersect(teamIds)
                .ToList();

            if (alreadyAssigned.Any())
                throw new InvalidOperationException(
                    $"Teams already assigned to another scorer: {string.Join(", ", alreadyAssigned)}");

            // Teams arrive in sheet order
            var ordered = teamIds
                .OrderBy(id => QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == id)?.SheetOrder ?? 0)
                .ToList();

            var assignment = new ScorerAssignment
            {
                ScorerId = scorerId,
                Label = label,
                TeamIds = ordered
            };

            round.Assignments.Add(assignment);
            return assignment;
        }

        public Round CreateRound(string name, int questionCount, List<Guid>? activeTeamIds = null)
        {
            var round = new Round
            {
                Name = name,
                QuestionCount = questionCount,
                ActiveTeamIds = activeTeamIds
                    ?? QuizNight.MasterTeamList
                        .OrderBy(t => t.SheetOrder)
                        .Select(t => t.Id)
                        .ToList()
            };

            QuizNight.Rounds.Add(round);
            _state?.SetActiveRound(round.Id);
            return round;
        }

        public void DeleteRound(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            QuizNight.Rounds.Remove(round);
        }

        public void FinalizeRound(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            round.IsFinalized = true;
        }

        public ScorerAssignment? GetAssignment(string scorerId)
        {
            return QuizNight
                .GetAssignment(_state.ActiveRoundId, scorerId);
        }

        public List<LeaderboardEntry> GetLeaderboard() => QuizNight.GetLeaderboard();

        public (int Recorded, int Expected, double PercentComplete) GetRoundProgress(Guid roundId)
        {
            var round = GetRoundOrThrow(roundId);
            var expected = round.ActiveTeamIds.Count * round.QuestionCount;
            var recorded = round.Answers.ToList().Count; // snapshot to avoid race
            var pct = expected == 0 ? 0d : (double)recorded / expected * 100;
            return (recorded, expected, Math.Round(pct, 1));
        }

        public Task InitializeAsync(QuizNight quizNight)
        {
            _state = new QuizSessionState(quizNight);
            return Task.CompletedTask;
        }

        public Answer RecordAnswerBool(string scorerId, Guid teamId, int questionIndex, bool correct)
        {
            if (_state == null)
                throw new InvalidOperationException("QuizNightService not initialized. Call InitializeAsync() first.");

            return _state.RecordAnswer(
                _state.ActiveRoundId,
                scorerId,
                teamId,
                questionIndex,
                new AnswerBool { Correct = correct });
        }

        public Answer RecordAnswerPoint(string scorerId, Guid teamId, int questionIndex, decimal points)
        {
            return _state.RecordAnswer(
                _state.ActiveRoundId,
                scorerId,
                teamId,
                questionIndex,
                new AnswerPoint { Points = points });
        }

        public void RemoveScorer(Guid roundId, string scorerId)
        {
            var round = GetRoundOrThrow(roundId);
            var assignment = round.Assignments.FirstOrDefault(a => a.ScorerId == scorerId);
            if (assignment != null)
                round.Assignments.Remove(assignment);
        }

        public void RemoveTeamFromRound(Guid roundId, Guid teamId)
        {
            var round = GetRoundOrThrow(roundId);
            round.ActiveTeamIds.Remove(teamId);
        }

        public void ReorderTeams(List<Guid> orderedTeamIds)
        {
            for (var i = 0; i < orderedTeamIds.Count; i++)
            {
                var team = QuizNight.MasterTeamList.FirstOrDefault(t => t.Id == orderedTeamIds[i]);
                if (team != null)
                    team.SheetOrder = i + 1;
            }
        }

        public Task SaveAsync() => _persistence.SaveAsync(QuizNight);

        public void SetAnswer(Guid roundId, Guid teamId, int questionIndex, bool? isCorrect)
        {
            var round = QuizNight.Rounds.First(r => r.Id == roundId);
            var existing = round.Answers.FirstOrDefault(a =>
                a.TeamId == teamId && a.QuestionIndex == questionIndex);

            if (isCorrect == null)
            {
                if (existing != null) round.Answers.Remove(existing);
                return;
            }

            var value = new AnswerBool { Correct = isCorrect.Value };

            if (existing != null)
                existing.Value = value;
            else
                round.Answers.Add(new Answer
                {
                    TeamId = teamId,
                    QuestionIndex = questionIndex,
                    Value = value,
                    RecordedByScorerId = "host"
                });
        }

        #endregion Public Methods

        #region Private Methods

        private Round GetRoundOrThrow(Guid roundId)
        {
            return QuizNight.Rounds.FirstOrDefault(r => r.Id == roundId)
                   ?? throw new ArgumentException($"Round '{roundId}' not found.");
        }

        #endregion Private Methods
    }
}
```


## FILE: Core\Services\ScorerSessionService.cs

```cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PubQuizMaster.Core.Services
{


    // ─────────────────────────────────────────────
    // SCORER SESSION SERVICE
    // Tracks which web clients are currently connected and maps them
    // to scorer assignments. Used by the SignalR hub.
    // Runtime only — not persisted.
    // ─────────────────────────────────────────────

    /// <summary>
    /// Manages active scorer connections (SignalR connection IDs → scorer IDs).
    /// Thread-safe. Used exclusively by the SignalR hub layer.
    /// </summary>
    public class ScorerSessionService
    {
        // connectionId → scorerId
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
            _connections = new();

        // scorerId → connectionId (reverse lookup)
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string>
            _scorers = new();

        /// <summary>Registers a new SignalR connection for a scorer.</summary>
        public void RegisterConnection(string connectionId, string scorerId)
        {
            // Remove any stale connection for this scorer
            if (_scorers.TryGetValue(scorerId, out var oldConnection))
                _connections.TryRemove(oldConnection, out _);

            _connections[connectionId] = scorerId;
            _scorers[scorerId] = connectionId;
        }

        /// <summary>Removes a connection on disconnect.</summary>
        public void RemoveConnection(string connectionId)
        {
            if (_connections.TryRemove(connectionId, out var scorerId))
                _scorers.TryRemove(scorerId, out _);
        }

        /// <summary>Returns the scorer ID for a connection ID, or null if unknown.</summary>
        public string? GetScorerId(string connectionId)
            => _connections.TryGetValue(connectionId, out var id) ? id : null;

        /// <summary>Returns the connection ID for a scorer ID, or null if not connected.</summary>
        public string? GetConnectionId(string scorerId)
            => _scorers.TryGetValue(scorerId, out var conn) ? conn : null;

        /// <summary>Returns all currently connected scorer IDs.</summary>
        public IEnumerable<string> ConnectedScorers => _scorers.Keys;

        /// <summary>Returns true if the given scorer currently has an active connection.</summary>
        public bool IsConnected(string scorerId) => _scorers.ContainsKey(scorerId);
    }
}
```


## FILE: Core\Services\SettingsService.cs

```cs
using System.Text.Json;
using PubQuizMaster.Desktop.Models;

namespace PubQuizMaster.Core.Services
{
    public class SettingsService
    {
        #region Private Fields

        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
        private readonly string _path;

        #endregion Private Fields

        #region Public Constructors

        public SettingsService()
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PubQuizMaster");

            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "settings.json");
        }

        #endregion Public Constructors

        #region Public Properties

        public Settings Settings { get; private set; } = new();

        #endregion Public Properties

        #region Public Methods

        public void Load()
        {
            if (!File.Exists(_path)) return;
            try
            {
                Settings = JsonSerializer.Deserialize<Settings>(
                    File.ReadAllText(_path), _json) ?? new();
            }
            catch { Settings = new(); }
        }

        public void Save()
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Settings, _json));
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\App.axaml

```axaml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="PubQuizMaster.Desktop.App"
             RequestedThemeVariant="Dark">

    <Application.Resources>
        <ResourceDictionary>
            <!-- ── Color Palette ── -->
            <Color x:Key="ColorBg">#1a1a2e</Color>
            <Color x:Key="ColorSurface">#16213e</Color>
            <Color x:Key="ColorCard">#0f3460</Color>
            <Color x:Key="ColorAccent">#e94560</Color>
            <Color x:Key="ColorCorrect">#2ecc71</Color>
            <Color x:Key="ColorWrong">#e74c3c</Color>
            <Color x:Key="ColorNeutral">#4a4a6a</Color>
            <Color x:Key="ColorText">#eaeaea</Color>
            <Color x:Key="ColorSubtext">#9999bb</Color>
            <Color x:Key="ColorHover">#1a2a4e</Color>

            <SolidColorBrush x:Key="BrushBg" Color="{StaticResource ColorBg}"/>
            <SolidColorBrush x:Key="BrushSurface" Color="{StaticResource ColorSurface}"/>
            <SolidColorBrush x:Key="BrushCard" Color="{StaticResource ColorCard}"/>
            <SolidColorBrush x:Key="BrushAccent" Color="{StaticResource ColorAccent}"/>
            <SolidColorBrush x:Key="BrushCorrect" Color="{StaticResource ColorCorrect}"/>
            <SolidColorBrush x:Key="BrushWrong" Color="{StaticResource ColorWrong}"/>
            <SolidColorBrush x:Key="BrushNeutral" Color="{StaticResource ColorNeutral}"/>
            <SolidColorBrush x:Key="BrushText" Color="{StaticResource ColorText}"/>
            <SolidColorBrush x:Key="BrushSubtext" Color="{StaticResource ColorSubtext}"/>
            <SolidColorBrush x:Key="BrushHover" Color="{StaticResource ColorHover}"/>
        </ResourceDictionary>
    </Application.Resources>

    <Application.Styles>
        <FluentTheme DensityStyle="Normal"/>

        <!-- ── Base Controls ── -->
        <Style Selector="TextBlock">
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
        </Style>

        <Style Selector="TextBox">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BrushNeutral}"/>
            <Setter Property="CornerRadius" Value="6"/>
            <Setter Property="Padding" Value="10 8"/>
        </Style>
        <Style Selector="TextBox:pointerover">
            <Setter Property="BorderBrush" Value="{StaticResource BrushSubtext}"/>
        </Style>
        <Style Selector="TextBox:focus">
            <Setter Property="BorderBrush" Value="{StaticResource BrushAccent}"/>
        </Style>

        <Style Selector="Button">
            <Setter Property="Background" Value="{StaticResource BrushCard}"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
            <Setter Property="CornerRadius" Value="6"/>
            <Setter Property="Padding" Value="12 8"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
        <Style Selector="Button:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushHover}"/>
        </Style>
        <Style Selector="Button.primary">
            <Setter Property="Background" Value="{StaticResource BrushAccent}"/>
        </Style>
        <Style Selector="Button.primary:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="#c73652"/>
        </Style>
        <Style Selector="Button.danger">
            <Setter Property="Background" Value="{StaticResource BrushWrong}"/>
        </Style>
        <Style Selector="Button.danger:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="#c0392b"/>
        </Style>
        <Style Selector="Button:disabled /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushNeutral}"/>
            <Setter Property="Opacity" Value="1"/>
        </Style>
        <Style Selector="Button:disabled">
            <Setter Property="Foreground" Value="#aaaacc"/>
        </Style>

        <Style Selector="ListBox">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BrushNeutral}"/>
            <Setter Property="CornerRadius" Value="6"/>
        </Style>
        <Style Selector="ListBoxItem">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
            <Setter Property="Padding" Value="10 8"/>
        </Style>
        <Style Selector="ListBoxItem:selected /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushCard}"/>
        </Style>
        <Style Selector="ListBoxItem:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushHover}"/>
        </Style>

        <Style Selector="CheckBox">
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
        </Style>

        <!-- ── CalendarDatePicker ── -->
        <Style Selector="CalendarDatePicker">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BrushNeutral}"/>
        </Style>
        <Style Selector="CalendarDatePicker:pointerover">
            <Setter Property="BorderBrush" Value="{StaticResource BrushSubtext}"/>
        </Style>

        <!-- Calendar popup background -->
        <Style Selector="CalendarItem">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BrushCard}"/>
        </Style>

        <!-- Header buttons (Monat/Jahr Navigation) -->
        <Style Selector="CalendarItem Button">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
        </Style>
        <Style Selector="CalendarItem Button:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushHover}"/>
        </Style>

        <!-- Wochentags-Header -->
        <Style Selector="CalendarDayButton">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
        </Style>
        <Style Selector="CalendarDayButton:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushHover}"/>
            <Setter Property="TextElement.Foreground" Value="{StaticResource BrushText}"/>
        </Style>
        <Style Selector="CalendarDayButton:pressed /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushCard}"/>
        </Style>
        <Style Selector="CalendarDayButton:selected /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushAccent}"/>
            <Setter Property="TextElement.Foreground" Value="{StaticResource BrushText}"/>
        </Style>
        <Style Selector="CalendarDayButton:inactive">
            <Setter Property="Foreground" Value="{StaticResource BrushNeutral}"/>
        </Style>
        <Style Selector="CalendarDayButton:today /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushCard}"/>
            <Setter Property="TextElement.Foreground" Value="{StaticResource BrushAccent}"/>
        </Style>

        <!-- Monats/Jahr-Auswahl -->
        <Style Selector="CalendarButton">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
        </Style>
        <Style Selector="CalendarButton:pointerover /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushHover}"/>
            <Setter Property="TextElement.Foreground" Value="{StaticResource BrushText}"/>
        </Style>
        <Style Selector="CalendarButton:selected /template/ ContentPresenter">
            <Setter Property="Background" Value="{StaticResource BrushAccent}"/>
        </Style>

        <!-- NumericUpDown -->
        <Style Selector="NumericUpDown">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
            <Setter Property="Foreground" Value="{StaticResource BrushText}"/>
            <Setter Property="BorderBrush" Value="{StaticResource BrushNeutral}"/>
        </Style>
        <Style Selector="NumericUpDown /template/ ButtonSpinner /template/ Border">
            <Setter Property="Background" Value="{StaticResource BrushSurface}"/>
        </Style>
    </Application.Styles>

</Application>

```


## FILE: Desktop\App.axaml.cs

```cs
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.Extensions;
using PubQuizMaster.Desktop.ViewModels;
using PubQuizMaster.Desktop.Views;
using PubQuizMaster.Desktop.Web;

namespace PubQuizMaster.Desktop
{
    public class App
        : Application
    {
        #region Public Properties

        public static KestrelHost KestrelHost { get; private set; } = null!;

        public static QuizNightService QuizNightService { get; private set; } = null!;

        public static SettingsService SettingsService { get; private set; } = null!;

        #endregion Public Properties

        #region Public Methods

        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override async void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // -- 1. Services -------------------------------------------------------
            var persistence = new PersistenceService();
            var scorerSession = new ScorerSessionService();
            QuizNightService = new QuizNightService(persistence);

            SettingsService = new SettingsService();
            SettingsService.Load();

            // -- 2. Startup dialog -------------------------------------------------
            var startupVm = new StartupViewModel(persistence);
            var startupWindow = new StartupWindow(startupVm);

            // ShowDialog blockiert bis CloseRequested() die Window schließt
            desktop.MainWindow = startupWindow;
            startupWindow.Show();

            // Warten bis der User eine Entscheidung getroffen hat
            await startupWindow.HasClosed();

            // Abbruch (Fenster geschlossen ohne Auswahl)
            if (startupVm.Result == null)
            {
                desktop.Shutdown();
                return;
            }

            // -- 3. Service initialisieren -----------------------------------------
            await QuizNightService.InitializeAsync(startupVm.Result);

            // -- 4. Kestrel --------------------------------------------------------
            KestrelHost = new KestrelHost(QuizNightService, persistence, scorerSession);
            KestrelHost.ServerReady += url => Console.WriteLine($"[PubQuizMaster] Server ready → {url}");

            // Pinggy optional aktivieren
            if (!string.IsNullOrWhiteSpace(SettingsService.Settings.PinggyToken) == false
                && string.IsNullOrWhiteSpace(SettingsService.Settings.ClientUrl))
            {
                // Kein Auto-Start hier — Pinggy wird durch den Button im StartupWindow gestartet
            }

            KestrelHost.Start();

            // -- 5. Main Window ----------------------------------------------------
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(QuizNightService, KestrelHost)
            };

            desktop.MainWindow = mainWindow;
            mainWindow.Show();

            desktop.ShutdownRequested += async (_, _) => await KestrelHost.StopAsync();

            base.OnFrameworkInitializationCompleted();
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\Extensions\WindowExtensions.cs

```cs
using System.Threading.Tasks;
using Avalonia.Controls;

namespace PubQuizMaster.Desktop.Extensions
{
    internal static class WindowExtensions
    {
        #region Public Methods

        public static Task HasClosed(this Window window)
        {
            var tcs = new TaskCompletionSource();
            window.Closed += (_, _) => tcs.TrySetResult();
            return tcs.Task;
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\Models\HostPhase.cs

```cs
// ===== MainWindowViewModel.cs =====
namespace PubQuizMaster.Desktop.Models
{
    public enum HostPhase { Scoring, Review }
}

```


## FILE: Desktop\Models\SavedNightEntry.cs

```cs
// ===== SavedNightEntry.cs =====
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.Models
{
    public partial class SavedNightEntry
        : ObservableObject
    {
        #region Public Constructors

        public SavedNightEntry(FileInfo file)
        {
            FilePath = file.FullName;
            DisplayName = Path.GetFileNameWithoutExtension(file.Name);
            LastModified = file.LastWriteTime.ToString("dd.MM.yyyy HH:mm");
        }

        #endregion Public Constructors

        #region Public Properties

        public string DisplayName { get; }

        public string FilePath { get; }

        public string LastModified { get; }

        #endregion Public Properties
    }
}
```


## FILE: Desktop\Models\TeamScoreEntry.cs

```cs
namespace PubQuizMaster.Desktop.Models
{
    public record TeamScoreEntry(string Name, decimal? Score);
}
```


## FILE: Desktop\Program.cs

```cs
using System;
using Avalonia;

namespace PubQuizMaster.Desktop
{
    internal sealed class Program
    {
        #region Public Methods

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args) => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

        #endregion Public Methods
    }
}
```


## FILE: Desktop\PubQuizMaster.Desktop.csproj

```csproj
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <AvaloniaResource Include="Assets\**" />
  </ItemGroup>

	<ItemGroup>
		<FrameworkReference Include="Microsoft.AspNetCore.App" />
	</ItemGroup>
	
	<ItemGroup>
    <PackageReference Include="Avalonia" Version="11.3.13" />
    <PackageReference Include="Avalonia.Desktop" Version="11.3.13" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.3.13" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.3.13" />
    <!--Condition below is needed to remove Avalonia.Diagnostics package from build output in Release configuration.-->
    <PackageReference Include="Avalonia.Diagnostics" Version="11.3.13">
      <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
      <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
    </PackageReference>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.2" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.5" />
    <PackageReference Include="QRCoder" Version="1.7.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\Core\PubQuizMaster.Core.csproj" />
  </ItemGroup>

	<ItemGroup>
		<Content Include="wwwroot\**\*">
			<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
		</Content>
	</ItemGroup>

</Project>

```


## FILE: Desktop\ViewLocator.cs

```cs
using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop
{
    /// <summary>
    /// Given a view model, returns the corresponding view if possible.
    /// </summary>
    [RequiresUnreferencedCode(
        "Default implementation of ViewLocator involves reflection which may be trimmed away.",
        Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
    public class ViewLocator : IDataTemplate
    {
        public Control? Build(object? param)
        {
            if (param is null)
                return null;

            var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
            var type = Type.GetType(name);

            if (type != null)
            {
                return (Control)Activator.CreateInstance(type)!;
            }

            return new TextBlock { Text = "Not Found: " + name };
        }

        public bool Match(object? data)
        {
            return data is ViewModelBase;
        }
    }
}

```


## FILE: Desktop\ViewModels\ActiveRoundViewModel.cs

```cs
using System.Collections.ObjectModel;
using System.Linq;
using PubQuizMaster.Core.Models.Event;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class ActiveRoundViewModel
        : ViewModelBase
    {
        #region Public Properties

        public string ActiveRoundName { get; private set; } = string.Empty;

        public string AnswerProgress => $"{AnswersRecorded} / {AnswersExpected}";

        public int AnswersExpected { get; private set; }

        public int AnswersRecorded { get; private set; }

        public ObservableCollection<ScorerStatusViewModel> ScorerStatuses { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public void Initialize(Round round, int recorded, int expected, string baseUrl = "")
        {
            ActiveRoundName = round.Name;
            Refresh(round, recorded, expected, baseUrl);

            OnPropertyChanged(nameof(ActiveRoundName));
        }

        public void Refresh(Round round, int recorded, int expected, string baseUrl = "")
        {
            AnswersRecorded = recorded;
            AnswersExpected = expected;
            OnPropertyChanged(nameof(AnswersRecorded));
            OnPropertyChanged(nameof(AnswersExpected));
            OnPropertyChanged(nameof(AnswerProgress));

            ScorerStatuses.Clear();
            foreach (var a in round.Assignments)
            {
                var answered = round.Answers.Count(ans => a.TeamIds.Contains(ans.TeamId));
                var url = string.IsNullOrEmpty(baseUrl)
                    ? "" : $"{baseUrl}?scorerId={a.ScorerId}";

                ScorerStatuses.Add(new ScorerStatusViewModel
                {
                    ScorerId = a.ScorerId,
                    Label = a.Label,
                    Answered = answered,
                    Expected = a.TeamIds.Count * round.QuestionCount,
                    QrCode = string.IsNullOrEmpty(url)
                        ? null : ScorerStatusViewModel.GenerateQrCode(url)
                });
            }
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\ViewModels\AnswerCellViewModel.cs

```cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class AnswerCellViewModel(bool? isCorrect)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool? _isCorrect = isCorrect;
        [ObservableProperty] private bool _isEditing;

        #endregion Private Fields

        #region Public Properties

        public string Color => IsCorrect switch
        {
            true => "#2ecc71",
            false => "#e74c3c",
            null => "#4a4a6a"
        };

        public string Display => IsCorrect switch
        {
            true => "✓",
            false => "✗",
            null => "–"
        };

        #endregion Public Properties

        #region Private Methods

        partial void OnIsCorrectChanged(bool? value)
        {
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(Color));
        }

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\AssignableTeamViewModel.cs

```cs
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class AssignableTeamViewModel(TeamViewModel teamVm, bool isAssigned = false)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool _isAssigned = isAssigned;
        [ObservableProperty] private bool _isDisabled;

        #endregion Private Fields

        #region Public Properties

        public Action<Guid, bool>? AssignmentChanged { get; set; }

        public string Name => TeamVm.Name;

        public Guid TeamId => TeamVm.Team.Id;

        public TeamViewModel TeamVm { get; } = teamVm;

        #endregion Public Properties

        #region Private Methods

        partial void OnIsAssignedChanged(bool value) =>
            AssignmentChanged?.Invoke(TeamVm.Team.Id, value);

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\CenterViewModel.cs

```cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class CenterViewModel
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private ViewModelBase? _currentContent;

        #endregion Private Fields

        #region Public Properties

        // Used by MainWindow to conditionally show the Start button
        public bool IsSetupMode => CurrentContent is SetupViewModel;

        #endregion Public Properties

        #region Public Methods

        public void ShowMatrix(RoundMatrixViewModel vm)
        {
            CurrentContent = vm;
            OnPropertyChanged(nameof(IsSetupMode));
        }

        public void ShowSetup(SetupViewModel vm)
        {
            CurrentContent = vm;
            OnPropertyChanged(nameof(IsSetupMode));
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\ViewModels\LeaderboardEntryViewModel.cs

```cs
namespace PubQuizMaster.Desktop.ViewModels
{
    public class LeaderboardEntryViewModel
    {
        #region Public Properties

        public int Rank { get; set; }

        public decimal Score { get; set; }

        public string TeamName { get; set; } = string.Empty;

        #endregion Public Properties
    }
}
```


## FILE: Desktop\ViewModels\LeftPanelViewModel.cs

```cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class LeftPanelViewModel(QuizNightService svc)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool _canAddRound;

        #endregion Private Fields

        #region Public Properties

        public Action? OnNewRound { get; set; }

        public Action<RoundEntryViewModel>? OnRoundSelected { get; set; }

        public ObservableCollection<RoundEntryViewModel> Rounds { get; } = [];

        public ObservableCollection<LeaderboardEntryViewModel> TotalBoard { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public void ClearSelection()
        {
            foreach (var r in Rounds) r.IsSelected = false;
        }

        public void Refresh(bool roundIsActive, bool setupIsActive = false)
        {
            CanAddRound = !roundIsActive && !setupIsActive
                && svc.QuizNight.Rounds.All(r => r.IsFinalized);

            Rounds.Clear();
            foreach (var r in svc.QuizNight.Rounds)
                Rounds.Add(new RoundEntryViewModel(
                    r, svc.QuizNight,
                    entry => OnRoundSelected?.Invoke(entry)));

            TotalBoard.Clear();
            var lb = svc.GetLeaderboard();
            int rank = 1;
            foreach (var e in lb)
                TotalBoard.Add(new LeaderboardEntryViewModel
                { Rank = rank++, TeamName = e.Team.Name, Score = e.TotalScore });
        }

        public void SelectEntry(Guid roundId)
        {
            foreach (var r in Rounds)
                r.IsSelected = r.RoundId == roundId;
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void NewRound() => OnNewRound?.Invoke();

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\MainWindowViewModel.cs

```cs
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Hub;
using PubQuizMaster.Core.Models.Contents;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.Models;
using PubQuizMaster.Desktop.Web;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class MainWindowViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly KestrelHost _kestrel;
        private readonly QuizNightService _svc;

        [ObservableProperty] private HostPhase _phase = HostPhase.Review;

        [ObservableProperty] private string _serverUrl = string.Empty;

        [ObservableProperty] private SetupViewModel _setup = null!;

        #endregion Private Fields

        #region Public Constructors

        public MainWindowViewModel()
            : this(App.QuizNightService, App.KestrelHost)
        { }

        public MainWindowViewModel(QuizNightService svc, KestrelHost kestrel)
        {
            _svc = svc;
            _kestrel = kestrel;

            QuizNightName = svc.QuizNight.Name;

            var savedClient = App.SettingsService.Settings.ClientUrl;
            ServerUrl = string.IsNullOrWhiteSpace(savedClient)
                ? $"http://{KestrelHost.GetLocalIpAddress()}:{KestrelHost.Port}"
                : savedClient;

            kestrel.ServerReady += url =>
            {
                if (string.IsNullOrWhiteSpace(App.SettingsService.Settings.ClientUrl))
                {
                    ServerUrl = url;
                }
            };

            kestrel.TunnelReady += url => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                ServerUrl = url;

                var round = _svc.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == _svc.ActiveRoundId);
                if (round != null && Phase == HostPhase.Scoring)
                {
                    var (recorded, expected, _) = _svc.GetRoundProgress(round.Id);
                    ActiveRound.Refresh(round, recorded, expected, url);
                }
            });

            LeftPanel = new LeftPanelViewModel(svc)
            {
                OnRoundSelected = OnRoundSelected,
                OnNewRound = ShowSetup
            };

            ShowSetup();

            svc.AnswerRecorded += OnAnswerRecorded;
            kestrel.ServerReady += url => ServerUrl = url;
        }

        #endregion Public Constructors

        #region Public Properties

        public ActiveRoundViewModel ActiveRound { get; } = new();

        public CenterViewModel Center { get; } = new();

        public bool IsReview => Phase == HostPhase.Review;

        public bool IsScoring => Phase == HostPhase.Scoring;

        public LeftPanelViewModel LeftPanel { get; private set; } = null!;

        public string? QuizNightName { get; }

        public bool ShowNewRoundButton => IsReview && !Center.IsSetupMode;

        public bool ShowStartButton => IsReview && Center.IsSetupMode;

        #endregion Public Properties

        #region Private Methods

        private RoundMatrixViewModel CreateMatrix(Round round)
        {
            var index = _svc.QuizNight.Rounds.IndexOf(round);
            var matrix = new RoundMatrixViewModel(round, index + 1, _svc);

            matrix.OnSaved = () => LeftPanel.Refresh(roundIsActive: false);
            matrix.OnDeleted = () =>
            {
                LeftPanel.Refresh(roundIsActive: false);
                ShowSetup();
            };

            return matrix;
        }

        [RelayCommand]
        private async Task FinalizeRound()
        {
            var round = _svc.QuizNight.Rounds
                .FirstOrDefault(r => r.Id == _svc.ActiveRoundId);
            if (round == null) return;

            _svc.FinalizeRound(round.Id);

            var (recorded, expected, _) = _svc.GetRoundProgress(round.Id);
            ActiveRound.Refresh(round, recorded, expected);

            var leaderboard = _svc.GetLeaderboard();

            if (_kestrel?.HubContext != null)
                await QuizHub.NotifyRoundFinalized(_kestrel.HubContext, round.Id, leaderboard);

            LeftPanel.Refresh(roundIsActive: false);
            LeftPanel.SelectEntry(round.Id);

            var matrix = CreateMatrix(round);

            Center.ShowMatrix(matrix);
            SwitchPhase(HostPhase.Review);
        }

        private void NotifyShowStartButton()
        {
            OnPropertyChanged(nameof(ShowStartButton));
            OnPropertyChanged(nameof(ShowNewRoundButton));
        }

        private void OnAnswerRecorded(Answer _)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var round = _svc.QuizNight.Rounds
                    .FirstOrDefault(r => r.Id == _svc.ActiveRoundId);
                if (round == null) return;

                var (recorded, expected, _) = _svc.GetRoundProgress(round.Id);
                ActiveRound.Refresh(round, recorded, expected, ServerUrl);
            });
        }

        private void OnRoundSelected(RoundEntryViewModel entry)
        {
            LeftPanel.SelectEntry(entry.RoundId);

            var round = _svc.QuizNight.Rounds.First(r => r.Id == entry.RoundId);
            var index = _svc.QuizNight.Rounds.IndexOf(round);

            var matrix = CreateMatrix(round);

            Center.ShowMatrix(matrix);
            NotifyShowStartButton();
        }

        [RelayCommand]
        private void OpenInBrowser()
        {
            if (string.IsNullOrEmpty(ServerUrl)) return;
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ServerUrl,
                UseShellExecute = true
            });
        }

        private void ShowSetup()
        {
            Setup = new SetupViewModel(_svc);
            Center.ShowSetup(Setup);
            LeftPanel.ClearSelection();
            LeftPanel.Refresh(roundIsActive: false, setupIsActive: true);
            NotifyShowStartButton();
        }

        [RelayCommand]
        private async Task StartRound()
        {
            if (!Setup.CanStartRound) return;

            var allTeamIds = Setup.Teams.Select(t => t.Team.Id).ToList();
            var round = _svc.CreateRound(Setup.RoundName, Setup.QuestionCount, allTeamIds);

            foreach (var a in Setup.Assignments)
            {
                var ids = a.SelectedTeams.Select(t => t.Team.Id).ToList();
                if (ids.Count > 0)
                    _svc.AssignScorer(round.Id, a.ScorerId, a.Label, ids);
            }

            if (_kestrel.HubContext != null)
                await QuizHub.NotifyRoundStarted(
                    _kestrel.HubContext, round, _svc.QuizNight.MasterTeamList);

            var (recorded, expected, _) = _svc.GetRoundProgress(round.Id);
            ActiveRound.Initialize(round, recorded, expected, ServerUrl);

            LeftPanel.Refresh(roundIsActive: true);
            SwitchPhase(HostPhase.Scoring);
        }

        private void SwitchPhase(HostPhase phase)
        {
            Phase = phase;

            OnPropertyChanged(nameof(IsScoring));
            OnPropertyChanged(nameof(IsReview));

            NotifyShowStartButton();
        }

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\RoundEntryViewModel.cs

```cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Desktop.Models;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class RoundEntryViewModel
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private bool _isSelected;

        #endregion Private Fields

        #region Public Constructors

        public RoundEntryViewModel(Round round, QuizNight quizNight, Action<RoundEntryViewModel> onSelect)
        {
            RoundId = round.Id;
            RoundName = round.Name;
            IsFinalized = round.IsFinalized;
            QuestionCount = round.QuestionCount;

            TeamScores = quizNight.MasterTeamList
                .Select(t => new TeamScoreEntry(t.Name, round.GetTeamScore(t.Id)))
                .Where(x => x.Score.HasValue)
                .OrderByDescending(x => x.Score)
                .ToList();

            QuestionCorrectCounts = Enumerable.Range(0, round.QuestionCount)
                .Select(qi => round.Answers.Count(a =>
                    a.QuestionIndex == qi && a.Value.GetScore() > 0))
                .ToList();

            SelectCommand = new RelayCommand(() => onSelect(this));
        }

        #endregion Public Constructors

        #region Public Properties

        public bool IsFinalized { get; }

        // How many teams answered each question correctly
        public IReadOnlyList<int> QuestionCorrectCounts { get; }

        public int QuestionCount { get; }

        public Guid RoundId { get; }

        public string RoundName { get; }

        public IRelayCommand SelectCommand { get; }

        public List<TeamScoreEntry> TeamScores { get; } = [];

        #endregion Public Properties
    }
}
```


## FILE: Desktop\ViewModels\RoundMatrixViewModel.cs

```cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class RoundMatrixViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly Round _round;
        private readonly QuizNightService _svc;

        [ObservableProperty] private bool _isEditing;

        #endregion Private Fields

        #region Public Constructors

        public RoundMatrixViewModel(Round round, int roundNumber, QuizNightService svc)
        {
            _round = round;
            _svc = svc;
            RoundName = round.Name;
            RoundNumber = roundNumber;

            QuestionHeaders = Enumerable.Range(1, round.QuestionCount)
                                        .Select(i => $"Q{i}")
                                        .ToList();
            Rebuild();
        }

        #endregion Public Constructors

        #region Public Properties

        public ObservableCollection<int> ColSums { get; } = [];

        public Action? ExportAction { get; set; }

        public Action? OnDeleted { get; set; }

        public Action? OnSaved { get; set; }

        public int QuestionCount => _round.QuestionCount;

        // Column headers Q1…Qn
        public IReadOnlyList<string> QuestionHeaders { get; }

        public string RoundName { get; }

        public int RoundNumber { get; }

        public ObservableCollection<TeamAnswerRowViewModel> Rows { get; } = new();

        #endregion Public Properties

        #region Public Methods

        public void Rebuild()
        {
            Rows.Clear();
            ColSums.Clear();

            var teams = _svc.QuizNight.MasterTeamList
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var team in teams)
            {
                var cells = Enumerable.Range(0, QuestionCount)
                    .Select(qi => new AnswerCellViewModel(
                        _round.GetAnswer(team.Id, qi)))
                    .ToList();
                Rows.Add(new TeamAnswerRowViewModel(team.Id, team.Name, cells));
            }

            // Column sums
            for (int qi = 0; qi < QuestionCount; qi++)
                ColSums.Add(Rows.Count(r => r.Answers[qi].IsCorrect == true));

            // Am Ende von Rebuild()
            foreach (var row in Rows)
                foreach (var cell in row.Answers)
                    cell.IsEditing = IsEditing;
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void Cancel()
        {
            Rebuild();
            IsEditing = false;
        }

        [RelayCommand]
        private async Task DeleteRound()
        {
            _svc.DeleteRound(_round.Id);
            await _svc.SaveAsync();
            OnDeleted?.Invoke();
        }

        [RelayCommand]
        private void Edit() => IsEditing = true;

        [RelayCommand]
        private void Export() => ExportAction?.Invoke();

        partial void OnIsEditingChanged(bool value)
        {
            foreach (var row in Rows)
                foreach (var cell in row.Answers)
                    cell.IsEditing = value;
        }

        [RelayCommand]
        private void Save()
        {
            // Persist edited answers back to the service
            foreach (var row in Rows)
                for (int qi = 0; qi < QuestionCount; qi++)
                    _svc.SetAnswer(_round.Id, row.TeamId, qi, row.Answers[qi].IsCorrect);

            Rebuild();
            IsEditing = false;

            OnSaved?.Invoke();
        }

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\ScorerAssignmentViewModel.cs

```cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class ScorerAssignmentViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly HashSet<Guid> _globalAssigned;
        private readonly ObservableCollection<TeamViewModel> _masterTeams;
        [ObservableProperty] private string _label;
        [ObservableProperty] private string _scorerId;

        #endregion Private Fields

        #region Public Constructors

        public ScorerAssignmentViewModel(string scorerId, string label,
            ObservableCollection<TeamViewModel> masterTeams,
            HashSet<Guid> globalAssigned)
        {
            _scorerId = scorerId;
            _label = label;
            _masterTeams = masterTeams;
            _globalAssigned = globalAssigned;

            foreach (var t in masterTeams)
                AddSlot(t, false);

            _masterTeams.CollectionChanged += OnMasterTeamsChanged;
        }

        #endregion Public Constructors

        #region Public Properties

        public Action<Guid, bool>? OnAssignmentChanged { get; set; }


        public Action? LabelEdited { get; set; }

        partial void OnLabelChanged(string value) => LabelEdited?.Invoke();

        public IEnumerable<TeamViewModel> SelectedTeams => Teams
            .Where(t => t.IsAssigned)
            .Select(t => t.TeamVm);

        public ObservableCollection<AssignableTeamViewModel> Teams { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public void ClearSelections()
        {
            foreach (var t in Teams.Where(t => t.IsAssigned))
                t.IsAssigned = false;
        }

        public void SetAssigned(Guid teamId, bool value)
        {
            var slot = Teams.FirstOrDefault(t => t.TeamVm.Team.Id == teamId);
            if (slot != null) slot.IsAssigned = value;
        }

        public void UpdateDisabledStates()
        {
            foreach (var t in Teams)
                t.IsDisabled = !t.IsAssigned && _globalAssigned.Contains(t.TeamVm.Team.Id);
        }

        #endregion Public Methods

        #region Private Methods

        private void AddSlot(TeamViewModel t, bool isAssigned)
        {
            var slot = new AssignableTeamViewModel(t, isAssigned)
            {
                AssignmentChanged = (id, assigned) => OnAssignmentChanged?.Invoke(id, assigned)
            };
            Teams.Add(slot);
        }

        private void OnMasterTeamsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TeamViewModel t in e.NewItems)
                    AddSlot(t, false);

            if (e.OldItems != null)
                foreach (TeamViewModel t in e.OldItems)
                {
                    var slot = Teams.FirstOrDefault(s => s.TeamVm.Team.Id == t.Team.Id);
                    if (slot != null)
                    {
                        if (slot.IsAssigned)
                            OnAssignmentChanged?.Invoke(t.Team.Id, false);
                        Teams.Remove(slot);
                    }
                }

            UpdateDisabledStates();
        }

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\ScorerStatusViewModel.cs

```cs
using Avalonia.Media.Imaging;
using QRCoder;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class ScorerStatusViewModel
    {
        #region Public Properties

        public int Answered { get; set; }

        public int Expected { get; set; }

        public bool IsComplete => Answered >= Expected;

        public string Label { get; set; } = string.Empty;

        public string Progress => $"{Answered} / {Expected}";

        public Bitmap? QrCode { get; set; }

        public string ScorerId { get; set; } = string.Empty;

        #endregion Public Properties

        #region Public Methods

        public static Bitmap GenerateQrCode(string url)
        {
            using var gen = new QRCodeGenerator();
            var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var png = new PngByteQRCode(data);
            var bytes = png.GetGraphic(6);
            using var ms = new System.IO.MemoryStream(bytes);
            return new Bitmap(ms);
        }

        #endregion Public Methods
    }
}
```


## FILE: Desktop\ViewModels\SetupViewModel.cs

```cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Services;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class SetupViewModel
        : ViewModelBase
    {
        #region Private Fields

        private readonly HashSet<Guid> _assignedTeamIds = [];
        private readonly QuizNightService _svc;

        [ObservableProperty] private string _newTeamName = string.Empty;
        [ObservableProperty] private int _questionCount = 6;
        [ObservableProperty] private string _roundName = string.Empty;
        [ObservableProperty] private string _setupError = string.Empty;

        #endregion Private Fields

        #region Public Constructors

        public SetupViewModel(QuizNightService svc)
        {
            _svc = svc;

            foreach (var t in svc.QuizNight.MasterTeamList.OrderBy(t => t.Name))
                Teams.Add(new TeamViewModel(t));

            RoundName = $"Round {svc.QuizNight.Rounds.Count + 1}";

            AddScorerInternal();
            AutoDistributeTeams();
        }

        #endregion Public Constructors

        #region Public Properties

        public ObservableCollection<ScorerAssignmentViewModel> Assignments { get; } = [];

        public bool CanStartRound
        {
            get
            {
                if (!Teams.Any() || !Assignments.Any()) return false;

                var allAssigned = Assignments.SelectMany(a => a.SelectedTeams).Select(t => t.Team.Id).ToList();
                if (allAssigned.Count != Teams.Count || allAssigned.Distinct().Count() != Teams.Count)
                    return false;

                if (_svc.QuizNight.Rounds.Any(r => r.Name.Equals(RoundName.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return false;

                var labels = Assignments.Select(a => a.Label.Trim()).ToList();
                if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() != labels.Count)
                {
                    SetupError = "Scorer labels must be unique.";
                    return false;
                }

                if (SetupError == "Scorer labels must be unique.")
                    SetupError = string.Empty;

                return true;
            }
        }

        public ObservableCollection<TeamViewModel> Teams { get; } = [];

        #endregion Public Properties

        #region Public Methods

        public void PrepareForNextRound()
        {
            RoundName = $"Round {_svc.QuizNight.Rounds.Count + 1}";
            QuestionCount = 6;
            SetupError = string.Empty;

            foreach (var a in Assignments)
                a.ClearSelections();
            _assignedTeamIds.Clear();

            AutoDistributeTeams();
            NotifyStartButton();
        }

        #endregion Public Methods

        #region Private Methods

        [RelayCommand]
        private void AddScorer()
        {
            AddScorerInternal();
            AutoDistributeTeams();
            NotifyStartButton();
        }

        private void AddScorerInternal()
        {
            var n = Assignments.Count;
            var id = $"scorer-{(char)('a' + n)}";
            var label = $"Scorer {(char)('A' + n)}";

            var vm = new ScorerAssignmentViewModel(id, label, Teams, _assignedTeamIds)
            {
                OnAssignmentChanged = OnScorerAssignmentChanged,
                LabelEdited = NotifyStartButton
            };

            Assignments.Add(vm);
        }

        [RelayCommand]
        private void AddTeam()
        {
            var name = NewTeamName.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            if (Teams.Any(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                SetupError = $"Team \"{name}\" already exists.";
                return;
            }

            SetupError = string.Empty;
            var team = _svc.AddTeam(name);
            var vm = new TeamViewModel(team);
            NewTeamName = string.Empty;

            // Insert at correct alphabetical position — single event, no Clear
            var insertIndex = 0;
            while (insertIndex < Teams.Count &&
                   string.Compare(Teams[insertIndex].Name, name, StringComparison.OrdinalIgnoreCase) < 0)
                insertIndex++;
            Teams.Insert(insertIndex, vm);

            _svc.ReorderTeams(Teams.Select(t => t.Team.Id).ToList());

            AutoDistributeTeams();
            NotifyStartButton();
        }

        private void AutoDistributeTeams()
        {
            if (!Assignments.Any() || !Teams.Any()) return;
            _assignedTeamIds.Clear();
            foreach (var a in Assignments) a.ClearSelections();

            var teams = Teams.ToList();
            for (int i = 0; i < teams.Count; i++)
            {
                var scorer = Assignments[i % Assignments.Count];
                var slot = scorer.Teams.FirstOrDefault(t => t.TeamVm.Team.Id == teams[i].Team.Id);
                if (slot != null) slot.IsAssigned = true;
                _assignedTeamIds.Add(teams[i].Team.Id);
            }

            foreach (var a in Assignments)
                a.UpdateDisabledStates();
        }

        private void NotifyStartButton() => OnPropertyChanged(nameof(CanStartRound));

        partial void OnRoundNameChanged(string value)
        {
            if (_svc.QuizNight.Rounds.Any(r => r.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)))
                SetupError = $"Round \"{value.Trim()}\" already exists.";
            else if (SetupError.StartsWith("Round"))
                SetupError = string.Empty;

            NotifyStartButton();
        }

        private void OnScorerAssignmentChanged(Guid teamId, bool assigned)
        {
            if (assigned) _assignedTeamIds.Add(teamId);
            else _assignedTeamIds.Remove(teamId);
            foreach (var a in Assignments)
                a.UpdateDisabledStates();
            NotifyStartButton();
        }

        [RelayCommand]
        private void RemoveScorer(ScorerAssignmentViewModel vm)
        {
            Assignments.Remove(vm);
            foreach (var t in vm.Teams.Where(t => t.IsAssigned))
                _assignedTeamIds.Remove(t.TeamVm.Team.Id);
            foreach (var a in Assignments)
                a.UpdateDisabledStates();
            AutoDistributeTeams();
            NotifyStartButton();
        }

        [RelayCommand]
        private void RemoveTeam(TeamViewModel vm)
        {
            Teams.Remove(vm);
            _svc.QuizNight.MasterTeamList.Remove(vm.Team);
            _assignedTeamIds.Remove(vm.Team.Id);
            NotifyStartButton();
        }

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\StartupViewModel.cs

```cs
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Services;
using PubQuizMaster.Desktop.Models;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class StartupViewModel : ViewModelBase
    {
        #region Private Fields

        private readonly PersistenceService _persistence;
        private string _clientUrl = string.Empty;

        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private DateTime? _newNightDate = DateTime.Today;

        [NotifyCanExecuteChangedFor(nameof(CreateNewCommand))]
        [ObservableProperty] private string _newNightName = string.Empty;

        [ObservableProperty] private ConnectionMode _selectedMode = ConnectionMode.Local;

        [NotifyCanExecuteChangedFor(nameof(LoadCommand))]
        [ObservableProperty] private SavedNightEntry? _selectedNight;

        #endregion Private Fields

        #region Public Constructors

        public StartupViewModel(PersistenceService persistence)
        {
            _persistence = persistence;
            LoadSavedNights();

            // Start in Local mode — set local IP immediately
            _clientUrl = $"http://{Web.KestrelHost.GetLocalIpAddress()}:{Web.KestrelHost.Port}";
        }

        #endregion Public Constructors

        #region Public Properties

        public string ClientUrl
        {
            get => _clientUrl;
            set
            {
                if (SetProperty(ref _clientUrl, value))
                {
                    if (SelectedMode == ConnectionMode.Tunnel)
                    {
                        App.SettingsService.Settings.ClientUrl = value;
                        App.SettingsService.Save();
                    }
                    OnPropertyChanged(nameof(IsConnected));
                }
            }
        }

        public Action? CloseRequested { get; set; }
        public bool IsConnected => !string.IsNullOrWhiteSpace(ClientUrl);

        public bool IsLocalMode => SelectedMode == ConnectionMode.Local;

        public bool IsTunnelMode
        {
            get => SelectedMode == ConnectionMode.Tunnel;
            set
            {
                SelectedMode = value ? ConnectionMode.Tunnel : ConnectionMode.Local;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocalMode));
            }
        }

        public QuizNight? Result { get; private set; }

        public ObservableCollection<SavedNightEntry> SavedNights { get; } = new();

        #endregion Public Properties

        #region Private Methods

        private bool CanCreateNew() => !string.IsNullOrWhiteSpace(NewNightName);

        private bool CanLoad() => SelectedNight != null;

        [RelayCommand(CanExecute = nameof(CanCreateNew))]
        private void CreateNew()
        {
            Result = new QuizNight
            {
                Name = NewNightName.Trim(),
                Date = NewNightDate ?? DateTime.Today,
            };
            CloseRequested?.Invoke();
        }

        [RelayCommand(CanExecute = nameof(CanLoad))]
        private async Task Load()
        {
            if (SelectedNight == null) return;
            try
            {
                Result = await _persistence.LoadAsync(SelectedNight.FilePath);
                CloseRequested?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Could not load file: {ex.Message}";
            }
        }

        private void LoadSavedNights()
        {
            SavedNights.Clear();
            foreach (var file in _persistence.ListSavedNights())
                SavedNights.Add(new SavedNightEntry(file));
        }

        partial void OnSelectedModeChanged(ConnectionMode value)
        {
            if (value == ConnectionMode.Local)
            {
                ClientUrl = $"http://{Web.KestrelHost.GetLocalIpAddress()}:{Web.KestrelHost.Port}";
            }
            else
            {
                // Restore persisted tunnel URL if available, otherwise clear
                var saved = App.SettingsService.Settings.ClientUrl;
                ClientUrl = !string.IsNullOrWhiteSpace(saved) ? saved : string.Empty;
            }
        }

        #endregion Private Methods
    }
}
```


## FILE: Desktop\ViewModels\TeamAnswerRowViewModel.cs

```cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace PubQuizMaster.Desktop.ViewModels
{
    public class TeamAnswerRowViewModel(Guid teamId, string teamName,
        IReadOnlyList<AnswerCellViewModel> answers)
    {
        #region Public Properties

        public IReadOnlyList<AnswerCellViewModel> Answers { get; } = answers;

        public Guid TeamId { get; } = teamId;

        public string TeamName { get; } = teamName;

        public int Total => Answers.Count(a => a.IsCorrect == true);

        #endregion Public Properties
    }
}
```


## FILE: Desktop\ViewModels\TeamViewModel.cs

```cs
using CommunityToolkit.Mvvm.ComponentModel;
using PubQuizMaster.Core.Models.Participants;

namespace PubQuizMaster.Desktop.ViewModels
{
    public partial class TeamViewModel(Team team)
        : ViewModelBase
    {
        #region Private Fields

        [ObservableProperty] private string _name = team.Name;

        #endregion Private Fields

        #region Public Properties

        public Team Team { get; } = team;

        #endregion Public Properties
    }
}
```


## FILE: Desktop\ViewModels\ViewModelBase.cs

```cs
using CommunityToolkit.Mvvm.ComponentModel;

namespace PubQuizMaster.Desktop.ViewModels
{
    public abstract class ViewModelBase
        : ObservableObject
    { }
}
```


## FILE: Desktop\Views\ActiveRoundView.axaml

```axaml
<UserControl
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:PubQuizMaster.Desktop.ViewModels"
    x:Class="PubQuizMaster.Desktop.Views.ActiveRoundView"
    x:DataType="vm:ActiveRoundViewModel">

    <Grid RowDefinitions="Auto,*">

        <Border Grid.Row="0" Background="{StaticResource BrushSurface}" CornerRadius="8"
                Padding="16" Margin="0 0 0 12">
            <Grid ColumnDefinitions="*,Auto">
                <StackPanel>
                    <TextBlock Text="{Binding ActiveRoundName}"
                               FontSize="20" FontWeight="Bold"/>
                    <TextBlock Text="{Binding AnswerProgress}"
                               FontSize="14" Foreground="{StaticResource BrushSubtext}" Margin="0 4 0 0"/>
                </StackPanel>
                <TextBlock Grid.Column="1" Text="answers recorded"
                           Foreground="{StaticResource BrushSubtext}" VerticalAlignment="Center"/>
            </Grid>
        </Border>

        <ScrollViewer Grid.Row="1">
            <ItemsControl ItemsSource="{Binding ScorerStatuses}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel Orientation="Horizontal"/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate x:DataType="vm:ScorerStatusViewModel">

                        <Border Background="{StaticResource BrushSurface}" CornerRadius="8"
                                Padding="20" Margin="0 0 12 12" Width="280">
                            <StackPanel Spacing="6">
                                <TextBlock Text="{Binding Label}"
                                           FontWeight="Bold" FontSize="14"/>
                                <TextBlock Text="{Binding ScorerId}"
                                           Foreground="{StaticResource BrushSubtext}" FontSize="12"/>
                                <TextBlock Text="{Binding Progress}"
                                           FontSize="24" FontWeight="Bold"
                                           Foreground="{StaticResource BrushAccent}" Margin="0 8 0 0"/>
                                <TextBlock Text="✓ Done" Foreground="{StaticResource BrushCorrect}"
                                           IsVisible="{Binding IsComplete}"
                                           FontWeight="Bold"/>
                                <Image Source="{Binding QrCode}"
                                       Width="200" Height="200"
                                       Margin="0 8 0 0"
                                       IsVisible="{Binding QrCode,
                                       Converter={x:Static ObjectConverters.IsNotNull}}"
                                       HorizontalAlignment="Center"/>
                            </StackPanel>
                        </Border>

                    </DataTemplate>

                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>

    </Grid>
</UserControl>

```


## FILE: Desktop\Views\ActiveRoundView.axaml.cs

```cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PubQuizMaster.Desktop.Views;

public partial class ActiveRoundView : UserControl
{
    public ActiveRoundView()
    {
        InitializeComponent();
    }
}
```


## FILE: Desktop\Views\LeftPanelView.axaml

```axaml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:PubQuizMaster.Desktop.ViewModels"
             x:Class="PubQuizMaster.Desktop.Views.LeftPanelView"
             x:DataType="vm:LeftPanelViewModel">

    <DockPanel>

        <ScrollViewer>
            <StackPanel Spacing="16">

                <!-- Gesamtstand -->
                <StackPanel Spacing="6">
                    <TextBlock Text="TOTAL STANDINGS"
                               FontSize="11" FontWeight="Bold"
                               Foreground="{StaticResource BrushSubtext}" LetterSpacing="1"/>

                    <ItemsControl ItemsSource="{Binding TotalBoard}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="vm:LeaderboardEntryViewModel">
                                <Grid ColumnDefinitions="20,*,Auto" Margin="0 3">
                                    <TextBlock Grid.Column="0" Text="{Binding Rank}"
                                               Foreground="{StaticResource BrushNeutral}" FontSize="12"/>
                                    <TextBlock Grid.Column="1" Text="{Binding TeamName}"
                                               FontSize="13" TextTrimming="CharacterEllipsis"/>
                                    <TextBlock Grid.Column="2" Text="{Binding Score}"
                                               Foreground="{StaticResource BrushCorrect}" FontWeight="Bold" FontSize="13"/>
                                </Grid>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>

                <Border Height="1" Background="{StaticResource BrushCard}"/>

                <!-- Rundenliste -->
                <StackPanel Spacing="6">
                    <TextBlock Text="ROUNDS"
                               FontSize="11" FontWeight="Bold"
                               Foreground="{StaticResource BrushSubtext}" LetterSpacing="1"/>

                    <ItemsControl ItemsSource="{Binding Rounds}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="vm:RoundEntryViewModel">
                                <Border
                                    CornerRadius="8"
                                    Padding="10 8"
                                    Margin="0 3"
                                    Cursor="Hand"
                                    Classes.selected="{Binding IsSelected}"
                                    PointerPressed="OnRoundEntryPressed">

                                    <Border.Background>
                                        <SolidColorBrush Color="{StaticResource BrushCard}"/>
                                    </Border.Background>

                                    <Border.Styles>
                                        <Style Selector="Border.selected">
                                            <Setter Property="Background" Value="#1a3a6e"/>
                                            <Setter Property="BorderBrush" Value="{StaticResource BrushAccent}"/>
                                            <Setter Property="BorderThickness" Value="1"/>
                                        </Style>
                                    </Border.Styles>

                                    <StackPanel Spacing="6">
                                        <!-- Header: nur Name, kein Badge mehr -->
                                        <TextBlock Text="{Binding RoundName}"
                                                   FontWeight="Bold" FontSize="13"/>

                                        <!-- Per-Team Scores -->
                                        <ItemsControl
                                            ItemsSource="{Binding TeamScores}"
                                            IsVisible="{Binding IsFinalized}">
                                            <ItemsControl.ItemTemplate>
                                                <DataTemplate>
                                                    <Grid ColumnDefinitions="*,Auto">
                                                        <TextBlock Grid.Column="0"
                                                                   Text="{Binding Name}"
                                                                   FontSize="11"
                                                                   Foreground="{StaticResource BrushSubtext}"
                                                                   TextTrimming="CharacterEllipsis"/>
                                                        <TextBlock Grid.Column="1"
                                                                   Text="{Binding Score}"
                                                                   FontSize="11"
                                                                   Foreground="{StaticResource BrushAccent}"
                                                                   FontWeight="Bold"/>
                                                    </Grid>
                                                </DataTemplate>
                                            </ItemsControl.ItemTemplate>
                                        </ItemsControl>

                                        <!-- Per-Question Correct Counts -->
                                        <StackPanel Orientation="Horizontal" Spacing="4"
                                                    IsVisible="{Binding IsFinalized}">
                                            <ItemsControl ItemsSource="{Binding QuestionCorrectCounts}">
                                                <ItemsControl.ItemsPanel>
                                                    <ItemsPanelTemplate>
                                                        <StackPanel Orientation="Horizontal" Spacing="4"/>
                                                    </ItemsPanelTemplate>
                                                </ItemsControl.ItemsPanel>
                                                <ItemsControl.ItemTemplate>
                                                    <DataTemplate>
                                                        <Border Width="20" Height="20"
                                                                CornerRadius="4"
                                                                Background="{StaticResource BrushSurface}">
                                                            <TextBlock Text="{Binding}"
                                                                       FontSize="10"
                                                                       Foreground="{StaticResource BrushSubtext}"
                                                                       HorizontalAlignment="Center"
                                                                       VerticalAlignment="Center"/>
                                                        </Border>
                                                    </DataTemplate>
                                                </ItemsControl.ItemTemplate>
                                            </ItemsControl>
                                        </StackPanel>

                                    </StackPanel>
                                </Border>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </StackPanel>

            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</UserControl>

```


## FILE: Desktop\Views\LeftPanelView.axaml.cs

```cs
using Avalonia.Controls;
using Avalonia.Input;
using PubQuizMaster.Desktop.ViewModels;

namespace PubQuizMaster.Desktop.Views
{
    public partial class LeftPanelView 
        : UserControl
    {
        #region Public Constructors

        public LeftPanelView() => InitializeComponent();

        #endregion Public Constructors

        #region Private Methods

        private void OnRoundEntryPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border &&
                border.DataContext is RoundEntryViewModel vm)
                vm.SelectCommand.Execute(null);
        }

        #endregion Private Methods
    }
}
```


