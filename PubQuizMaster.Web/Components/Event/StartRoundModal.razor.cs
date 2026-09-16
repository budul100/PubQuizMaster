using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;
using PubQuizMaster.Services.Event;
using PubQuizMaster.Web.ViewModels;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class StartRoundModal
    {
        #region Private Fields

        private List<ScorerAssignmentViewModel> assignments = [];
        private bool isSubmitting;
        private int questionCount = 20;
        private string roundName = string.Empty;

        #endregion Private Fields

        #region Public Properties

        /// <summary>Validation or save error from the parent, shown inside the dialog.</summary>
        [Parameter] public string? ErrorMessage { get; set; }

        [Parameter] public bool IsOpen { get; set; }

        [Parameter] public EventCallback OnCanceled { get; set; }

        [Parameter] public EventCallback<RoundRequest> OnStartRound { get; set; }

        [Parameter] public Quiz? Quiz { get; set; }

        #endregion Public Properties

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

        private static void ToggleTeam(ScorerAssignmentViewModel scorer, Guid teamId, bool isAssigned)
        {
            if (isAssigned)
            {
                if (!scorer.TeamIds.Contains(teamId)) scorer.TeamIds.Add(teamId);
            }
            else
            {
                scorer.TeamIds.Remove(teamId);
            }
        }

        private void AddScorer()
        {
            var usedLabels = assignments
                .Select(a => a.Label.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // First free letter for a readable default label
            var letter = Enumerable.Range('A', 26)
                .Select(c => (char)c)
                .FirstOrDefault(c => !usedLabels.Contains($"Scorer {c}"));

            assignments.Add(new ScorerAssignmentViewModel
            {
                ScorerId = ScorerTokens.Create(),
                Label = letter == default ? $"Scorer {assignments.Count + 1}" : $"Scorer {letter}"
            });

            // The manual distribution stays; "Auto-Distribute" or the checkboxes assign teams to the new scorer
        }

        private void AutoDistributeTeams()
        {
            if (Quiz == null || assignments.Count == 0) return;

            foreach (var a in assignments) a.TeamIds.Clear();

            var orderedTeams = Quiz.ParticipatingTeams.OrderBy(pt => pt.SheetOrder).ToList();
            for (int i = 0; i < orderedTeams.Count; i++)
            {
                assignments[i % assignments.Count].TeamIds.Add(orderedTeams[i].TeamId);
            }
        }

        private async Task Cancel() => await OnCanceled.InvokeAsync();

        private void InitializeRoundDefaults()
        {
            if (Quiz == null) return;

            roundName = $"Round {Quiz.Rounds.Count + 1}";
            questionCount = 20;

            // Explicit ordering: the Rounds collection order is not guaranteed by EF
            var lastRound = Quiz.Rounds
                .Where(r => r.Assignments.Count > 0)
                .MaxBy(r => r.CreatedAt);

            if (lastRound != null)
            {
                assignments = lastRound.Assignments
                    .OrderBy(a => a.Label, StringComparer.OrdinalIgnoreCase)
                    .Select(a => new ScorerAssignmentViewModel
                    {
                        // Keep the token so open scorer tabs continue; replace legacy IDs like "scorer-a"
                        // Keep the token so open scorer tabs continue
                        ScorerId = a.ScorerId,
                        Label = a.Label,
                        TeamIds = a.TeamIds
                            .Where(tid => Quiz.ParticipatingTeams.Any(pt => pt.TeamId == tid)).ToList()
                    }).ToList();

                var assignedTeamIds = assignments.SelectMany(s => s.TeamIds).ToHashSet();
                var unassigned = Quiz.ParticipatingTeams
                    .Where(pt => !assignedTeamIds.Contains(pt.TeamId))
                    .OrderBy(pt => pt.SheetOrder)
                    .ToList();

                for (int i = 0; i < unassigned.Count; i++)
                {
                    assignments[i % assignments.Count].TeamIds.Add(unassigned[i].TeamId);
                }
            }
            else
            {
                assignments =
                [
                    new ScorerAssignmentViewModel { ScorerId = ScorerTokens.Create(), Label = "Scorer A" }
                ];

                AutoDistributeTeams();
            }
        }

        private void RemoveScorer(int index)
        {
            if (assignments.Count <= 1 || index >= assignments.Count) return;

            var orphanedTeamIds = assignments[index].TeamIds;
            assignments.RemoveAt(index);

            // Only the teams of the removed scorer move, the rest of the distribution stays as it is
            foreach (var teamId in orphanedTeamIds)
            {
                assignments.MinBy(a => a.TeamIds.Count)!.TeamIds.Add(teamId);
            }
        }

        private async Task SubmitAsync()
        {
            if (Quiz == null || isSubmitting) return;

            isSubmitting = true;
            try
            {
                // Validation happens in QuizService, the parent passes its message back as ErrorMessage
                var request = new RoundRequest(
                    Quiz.Id,
                    roundName,
                    questionCount,
                    [.. assignments.Select(a => a.ToRequest())]);

                await OnStartRound.InvokeAsync(request);
            }
            finally
            {
                isSubmitting = false;
            }
        }

        #endregion Private Methods
    }
}
