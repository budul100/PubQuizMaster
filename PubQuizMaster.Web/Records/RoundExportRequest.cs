using Microsoft.AspNetCore.Components.Forms;

namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// Export request raised by the round list. The presentation mode follows from the round itself.
    /// </summary>
    public record RoundExportRequest(
        Guid RoundId,
        IBrowserFile SourceFile);
}