namespace PubQuizMaster.Web.Records
{
    /// <summary>
    /// A named line of a line chart. Points align with the chart's x labels by index,
    /// null leaves a gap. Color is any CSS color, e.g. var(--bs-primary).
    /// </summary>
    public record ChartSeries(
        string Name,
        decimal?[] Values,
        string Color,
        string[]? Tooltips = null);
}
