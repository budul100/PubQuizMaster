using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Records.Import;

namespace PubQuizMaster.Web.Pages.Import
{
    public partial class LegacyImport
    {
        #region Private Fields

        private bool isProcessing;
        private IBrowserFile? selectedFile;
        private ImportSummary? summary;

        #endregion Private Fields

        #region Private Methods

        private async Task ExecuteImportAsync()
        {
            if (selectedFile == null) return;

            isProcessing = true;
            try
            {
                using var stream = selectedFile.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
                summary = await LegacyImportService.ImportFromExcelAsync(stream);
                ToastService.ShowSuccess(
                    $"Import finished: {summary.ResultsCreated} new, {summary.ResultsUpdated} updated, " +
                    $"{summary.ResultsUnchanged} unchanged results.");
            }
            catch (Exception ex)
            {
                ToastService.ShowError($"Import failed: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }

        private void HandleFileSelected(InputFileChangeEventArgs e)
        {
            selectedFile = e.File;
            summary = null;
        }

        #endregion Private Methods
    }
}