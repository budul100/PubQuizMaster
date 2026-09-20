using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class RoundListPanel
    {
        #region Private Fields

        private int inputVersion;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Round? ActiveRound { get; set; }

        [Parameter] public bool IsExporting { get; set; }

        [Parameter] public EventCallback<Round> OnDeleteRound { get; set; }

        [Parameter] public EventCallback<RoundExportRequest> OnExportPptx { get; set; }

        [Parameter] public EventCallback OnStartRoundClick { get; set; }

        [Parameter] public List<Round> Rounds { get; set; } = [];

        #endregion Public Properties

        #region Private Methods

        private async Task ExportAsync(Round round, InputFileChangeEventArgs e)
        {
            try
            {
                await OnExportPptx.InvokeAsync(new RoundExportRequest(round.Id, e.File));
            }
            finally
            {
                inputVersion++;
            }
        }

        #endregion Private Methods
    }
}
