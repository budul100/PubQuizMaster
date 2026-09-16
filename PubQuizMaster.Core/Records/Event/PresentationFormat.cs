namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// File format of an exported presentation, derived from the uploaded source file.
    /// </summary>
    public record PresentationFormat(
        string Extension,
        string ContentType);
}
