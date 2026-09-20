using Microsoft.AspNetCore.Components;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Core.Records.Event;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class ActiveScoringBanner
    {
        #region Public Properties

        [Parameter] public bool IsProcessing { get; set; }

        [Parameter] public EventCallback OnFinalizeRound { get; set; }

        [Parameter, EditorRequired] public Round Round { get; set; } = null!;

        #endregion Public Properties

        #region Private Properties

        /// <summary>Teams of the round, including late registrations without a scorer assignment.</summary>
        private int TeamCount => Round.GetTeamIds().Length;

        #endregion Private Properties

        #region Private Methods

        private string GetPositionText(ScorerStatus? status, Scorer assignment)
        {
            if (status == null)
            {
                return "Not connected";
            }

            // Status still refers to a previous round: scorer has not picked up the new one yet
            if (status.RoundId != Round.Id)
            {
                return "Waiting for round";
            }

            return status.Phase switch
            {
                ScoringType.Sorting => "Sorting sheets",
                ScoringType.Scoring =>
                    $"Q {status.QuestionIndex + 1}/{Round.Length} \u00B7 Sheet {status.TeamIndex + 1}/{assignment.TeamIds.Count}",
                ScoringType.Reviewing => "Reviewing overview",
                _ => "Idle"
            };
        }

        private string GetScorerStationUrl(string scorerId)
        {
            var baseUri = Nav.BaseUri.TrimEnd('/');
            return $"{baseUri}/scorer/{Uri.EscapeDataString(scorerId)}";
        }

        #endregion Private Methods
    }
}