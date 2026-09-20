namespace PubQuizMaster.Core.Records.Event
{
    /// <summary>
    /// Outcome of filling a presentation: the file format plus everything the template could not take.
    /// MissingShapes entries read "SlideName/ShapeName". Warnings cover content that did not fit,
    /// e.g. more tied winners than Team shapes on the slide.
    /// </summary>
    public record PresentationResult(
        PresentationFormat Format,
        string[] MissingSlides,
        string[] MissingShapes,
        string[] Warnings)
    {
        public bool HasIssues => MissingSlides.Length > 0 || MissingShapes.Length > 0 || Warnings.Length > 0;
    }
}