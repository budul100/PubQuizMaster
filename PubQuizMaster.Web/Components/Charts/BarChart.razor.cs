using System.Globalization;
using Microsoft.AspNetCore.Components;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Charts
{
    /// <summary>
    /// Plain SVG bar chart, rendered on the server without any script.
    /// An optional reference value draws a dashed line, e.g. the average.
    /// </summary>
    public partial class BarChart
    {
        #region Private Fields

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string AriaLabel { get; set; } = "Bar chart";

        [Parameter] public string Color { get; set; } = "var(--bs-primary)";

        [Parameter] public string EmptyText { get; set; } = "No data yet.";

        [Parameter] public double Height { get; set; } = 200;

        [Parameter] public string HighlightColor { get; set; } = "var(--bs-success)";

        /// <summary>Fixed top of the y axis, e.g. 1 for rates. Otherwise derived from the data.</summary>
        [Parameter] public decimal? Max { get; set; }

        [Parameter] public ChartPoint[] Points { get; set; } = [];

        [Parameter] public string? ReferenceLabel { get; set; }

        [Parameter] public decimal? ReferenceValue { get; set; }

        /// <summary>.NET format string for axis and values, e.g. "0%" or "0.#".</summary>
        [Parameter] public string ValueFormat { get; set; } = "0.#";

        /// <summary>Prints the value above every bar. Only for a handful of bars.</summary>
        [Parameter] public bool ShowValues { get; set; } = true;

        /// <summary>
        /// Width of the drawing in viewBox units. The chart always fills its container, a larger value
        /// keeps a wide chart flat: the rendered height is Height scaled by container width / ViewWidth.
        /// </summary>
        [Parameter] public double ViewWidth { get; set; } = ChartLayout.Width;

        #endregion Public Properties

        #region Private Properties

        private decimal AxisMax => Max ?? ChartLayout.NiceMax(Points.Select(p => p.Value)
            .Append(ReferenceValue ?? 0m)
            .Max());

        #endregion Private Properties
    }
}
