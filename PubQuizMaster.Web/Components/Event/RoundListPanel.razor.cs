using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Enums;
using PubQuizMaster.Core.Models.Event;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Event
{
    public partial class RoundListPanel
    {
        #region Private Fields

        // Changes after each export, so the file inputs are recreated
        private int inputVersion;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public Round? ActiveRound { get; set; }

        [Parameter] public bool IsExporting { get; set; }

        [Parameter] public EventCallback<RoundExportRequest> OnExportPptx { get; set; }

        [Parameter] public EventCallback OnStartRoundClick { get; set; }

        [Parameter] public List<Round> Rounds { get; set; } = [];

        #endregion Public Properties

        #region Private Methods

        private async Task ExportAsync(Guid roundId, PresentationMode mode, InputFileChangeEventArgs e)
        {
            try
            {
                // The file is read inside the callback, the input must still exist at that point
                await OnExportPptx.InvokeAsync(new RoundExportRequest(roundId, mode, e.File));
            }
            finally
            {
                // Browsers raise no change event when the same file is picked again on the same input
                inputVersion++;
            }
        }

        #endregion Private Methods
    }
}
