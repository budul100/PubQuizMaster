using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Extensions;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Web.Helpers;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class RoundModal
    {
        #region Private Fields

        private List<ScorerAssignment> assignments = [];
        private int? contentPosition;
        private bool isFinalRound;
        private bool isSubmitting;
        private int questionCount = 20;
        private string roundName = string.Empty;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string? ErrorMessage { get; set; }

        [Parameter] public bool IsOpen { get; set; }

        [Parameter] public EventCallback OnCanceled { get; set; }

        [Parameter] public EventCallback<RoundRequest> OnStartRound { get; set; }

        [Parameter] public Quiz? Quiz { get; set; }

        #endregion Public Properties

        #region Private Properties

        /// <summary>Active teams of the night in sheet order, i.e. alphabetical.</summary>
        private Participant[] ActiveParticipants => Quiz?.ParticipatingTeams
            .Where(p => p.IsActive)
            .OrderBy(p => p.Team.Name, TeamNameComparer.Instance)
            .ToArray() ?? [];

        private Dictionary<Guid, string> TeamNames => Quiz?.ParticipatingTeams
            .ToDictionary(p => p.TeamId, p => p.Team.Name) ?? new();

        #endregion Private Properties

        #region Protected Methods

        protected override void OnParametersSet()
        {
            if (IsOpen && Quiz != null && assignments.Count == 0)
            {
                InitializeRoundDefaults();
            }
            else if (!IsOpen)
            {
                assignments.Clear();
            }
        }

        #endregion Protected Methods

        #region Private Methods

        private void AddScorer()
        {
            var usedLabels = assignments
                .Select(a => a.Label.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var letter = Enumerable.Range('A', 26)
                .Select(c => (char)c)
                .FirstOrDefault(c => !usedLabels.Contains($"Scorer {c}"));

            assignments.Add(new ScorerAssignment
            {
                ScorerId = ScorerTokens.Create(),
                Label = letter == default
                    ? $"Scorer {assignments.Count + 1}"
                    : $"Scorer {letter}"
            });

            AutoDistributeTeams();
        }

        /// <summary>
        /// Strictly alphabetical: stations in label order, each gets a contiguous block of team names.
        /// </summary>
        private void AutoDistributeTeams()
        {
            if (Quiz == null || assignments.Count == 0) return;

            assignments = [.. assignments.OrderBy(a => a.Label, TeamNameComparer.Instance)];

            var blocks = TeamDistribution.Split(
                items: ActiveParticipants,
                name: p => p.Team.Name,
                stationCount: assignments.Count);

            for (var i = 0; i < assignments.Count; i++)
            {
                assignments[i].TeamIds = [.. blocks[i].Select(p => p.TeamId)];
            }
        }

        private async Task Cancel() => await OnCanceled.InvokeAsync();

        private void HandleContentRoundChanged(ChangeEventArgs e)
        {
            SelectContentRound(int.TryParse(e.Value?.ToString(), out var position) ? position : null);
        }

        private void InitializeRoundDefaults()
        {
            if (Quiz == null) return;

            var existingNames = Quiz.Rounds.Select(r => r.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var nextNum = Quiz.Rounds.Count + 1;
            while (existingNames.Contains($"Round {nextNum}"))
            {
                nextNum++;
            }

            roundName = $"Round {nextNum}";

            var lastRound = Quiz.Rounds
                .Where(r => r.Assignments.Count > 0)
                .MaxBy(r => r.CreatedAt);

            questionCount = lastRound?.Length ?? 20;
            isFinalRound = false;

            // Suggest the first round of the uploaded questions that no round has used yet
            var usedPositions = Quiz.Rounds
                .Select(r => r.Position)
                .OfType<int>()
                .ToHashSet();

            var nextContentRound = Quiz.Content?.Rounds
                .FirstOrDefault(r => !usedPositions.Contains(r.Position));

            SelectContentRound(nextContentRound?.Position);

            if (lastRound != null && HasSameStations(lastRound))
            {
                KeepPreviousBlocks(lastRound);
            }
            else
            {
                StartFromStations();
            }
        }

        /// <summary>
        /// Whether the stations of the night are still the ones of the given round.
        /// Nights from before stations existed have none and count as unchanged.
        /// </summary>
        private bool HasSameStations(Round round)
        {
            if (Quiz == null || Quiz.Stations.Count == 0) return true;

            var stationIds = Quiz.Stations
                .Select(s => s.ScorerId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return stationIds.SetEquals(round.Assignments.Select(a => a.ScorerId));
        }

        /// <summary>
        /// Scorers keep their block of the previous round. Teams without a station are inserted
        /// into the station whose alphabetical range covers them.
        /// </summary>
        private void KeepPreviousBlocks(Round lastRound)
        {
            var activeTeamIds = ActiveParticipants
                .Select(p => p.TeamId)
                .ToHashSet();

            assignments = [.. lastRound.Assignments
                .OrderBy(a => a.Label, TeamNameComparer.Instance)
                .Select(a => new ScorerAssignment
                {
                    ScorerId = a.ScorerId,
                    Label = a.Label,
                    TeamIds = [.. a.TeamIds.Where(activeTeamIds.Contains)]
                })];

            var teamNames = TeamNames;

            var assignedTeamIds = assignments
                .SelectMany(s => s.TeamIds)
                .ToHashSet();

            var unassigned = ActiveParticipants
                .Where(p => !assignedTeamIds.Contains(p.TeamId))
                .ToArray();

            foreach (var participant in unassigned)
            {
                var stationNames = assignments
                    .Select(a => a.TeamIds
                        .Select(id => teamNames.GetValueOrDefault(id))
                        .OfType<string>()
                        .ToArray())
                    .ToArray();

                var index = TeamDistribution.FindStation(stationNames, participant.Team.Name);
                assignments[index].TeamIds.Add(participant.TeamId);
            }

            foreach (var assignment in assignments)
            {
                SortTeamIds(assignment);
            }
        }

        private void RemoveScorer(int index)
        {
            if (assignments.Count <= 1 || index >= assignments.Count) return;

            assignments.RemoveAt(index);
            AutoDistributeTeams();
        }

        /// <summary>
        /// Links the round to a round of the uploaded questions. The question count follows the content;
        /// the last content round is suggested as final round, the checkbox stays editable.
        /// </summary>
        private void SelectContentRound(int? position)
        {
            var content = Quiz?.Content;
            var contentRound = content?.GetRound(position);

            contentPosition = contentRound?.Position;

            if (content == null || contentRound == null) return;

            questionCount = contentRound.Questions.Count;
            isFinalRound = content.Rounds.Count > 1 && contentRound == content.Rounds[^1];
        }

        private void SortTeamIds(ScorerAssignment assignment)
        {
            var teamNames = TeamNames;

            assignment.TeamIds = [.. assignment.TeamIds
                .OrderBy(id => teamNames.GetValueOrDefault(id, string.Empty), TeamNameComparer.Instance)];
        }

        /// <summary>
        /// First round or changed stations: the stations of the night, or a single default station,
        /// with a fresh alphabetical distribution.
        /// </summary>
        private void StartFromStations()
        {
            if (Quiz == null) return;

            assignments = [.. Quiz.Stations.Select(s => new ScorerAssignment
            {
                ScorerId = s.ScorerId,
                Label = s.Label
            })];

            if (assignments.Count == 0)
            {
                assignments.Add(new ScorerAssignment
                {
                    ScorerId = ScorerTokens.Create(),
                    Label = "Scorer A"
                });
            }

            AutoDistributeTeams();
        }

        private async Task SubmitAsync()
        {
            if (Quiz == null || isSubmitting) return;

            isSubmitting = true;

            try
            {
                var roundAssignments = assignments
                    .Select(a => a.ToRequest()).ToList();

                var request = new RoundRequest(
                    QuizId: Quiz.Id,
                    RoundName: roundName,
                    Length: questionCount,
                    IsFinal: isFinalRound,
                    Assignments: roundAssignments,
                    ContentPosition: contentPosition);

                await OnStartRound.InvokeAsync(request);
            }
            finally
            {
                isSubmitting = false;
            }
        }

        private void ToggleTeam(ScorerAssignment scorer, Guid teamId, bool isAssigned)
        {
            if (isAssigned)
            {
                if (!scorer.TeamIds.Contains(teamId))
                {
                    scorer.TeamIds.Add(teamId);
                    SortTeamIds(scorer);
                }
            }
            else
            {
                scorer.TeamIds.Remove(teamId);
            }
        }

        #endregion Private Methods
    }
}
