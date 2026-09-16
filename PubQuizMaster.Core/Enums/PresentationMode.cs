namespace PubQuizMaster.Core.Enums
{
    /// <summary>
    /// Selects which slides the PowerPoint export shows.
    /// </summary>
    public enum PresentationMode
    {
        /// <summary>
        /// Round results plus the overall standings up to this round.
        /// </summary>
        Round,

        /// <summary>
        /// Round results plus the final podium and the closing slide.
        /// </summary>
        Final
    }
}
