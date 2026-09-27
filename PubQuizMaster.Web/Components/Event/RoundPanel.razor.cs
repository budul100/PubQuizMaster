using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class RoundPanel
    {
        #region Private Fields

        private int inputVersion;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Round? ActiveRound { get; set; }

        /// <summary>Uploaded questions of the quiz night, null if none were uploaded.</summary>
        [Parameter] public Core.Models.Content.Quiz? Content { get; set; }

        [Parameter] public bool IsExporting { get; set; }

        [Parameter] public EventCallback<Round> OnDeleteRound { get; set; }

        [Parameter] public EventCallback<ExportRequest> OnExportPptx { get; set; }

        [Parameter] public EventCallback OnStartRoundClick { get; set; }

        [Parameter] public List<Round> Rounds { get; set; } = [];

        #endregion Public Properties

        #region Private Properties

        /// <summary>Uploaded rounds that no live round is linked to yet, in position order.</summary>
        private Core.Models.Content.Round[] PlannedRounds => Content?.Rounds
            .Where(c => !Rounds.Any(r => r.Position == c.Position))
            .ToArray() ?? [];

        #endregion Private Properties

        #region Private Methods

        private async Task ExportAsync(Round round, InputFileChangeEventArgs e)
        {
            try
            {
                await OnExportPptx.InvokeAsync(new ExportRequest(round.Id, e.File));
            }
            finally
            {
                inputVersion++;
            }
        }

        #endregion Private Methods
    }
}
