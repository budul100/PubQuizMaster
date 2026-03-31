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