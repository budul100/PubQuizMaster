namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// One value of a chart. The tooltip shows on hover, highlighted points are drawn in the accent color.
    /// </summary>
    public record ChartPoint(
        string Label,
        decimal Value,
        string? Tooltip = null,
        bool IsHighlighted = false);
}
