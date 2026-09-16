using Microsoft.AspNetCore.Components.Forms;
using PubQuizMaster.Core.Enums;

namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// Export request raised by the round list.
    /// </summary>
    public record RoundExportRequest(
        Guid RoundId,
        PresentationMode Mode,
        IBrowserFile SourceFile);
}
