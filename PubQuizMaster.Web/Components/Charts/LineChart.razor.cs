using System.Globalization;
using Microsoft.AspNetCore.Components;
using PubQuizMaster.Web.Records;

namespace PubQuizMaster.Web.Components.Charts
{
    /// <summary>
    /// Plain SVG line chart, rendered on the server without any script.
    /// Every series has one value per x label, null values leave a gap in the dots
    /// and are skipped by the line.
    /// </summary>
    public partial class LineChart
    {
        #region Private Fields

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        #endregion Private Fields

        #region Public Properties

        [Parameter] public string AriaLabel { get; set; } = "Line chart";

        [Parameter] public string EmptyText { get; set; } = "No data yet.";

        [Parameter] public double Height { get; set; } = 200;

        [Parameter] public string[] Labels { get; set; } = [];

        /// <summary>Fixed top of the y axis. Otherwise derived from the data.</summary>
        [Parameter] public decimal? Max { get; set; }

        [Parameter] public ChartSeries[] Series { get; set; } = [];

        /// <summary>.NET format string for axis and tooltips, e.g. "0.#".</summary>
        [Parameter] public string ValueFormat { get; set; } = "0.#";

        #endregion Public Properties

        #region Private Properties

        private decimal AxisMax => Max ?? ChartLayout.NiceMax(Series
            .SelectMany(s => s.Values)
            .OfType<decimal>()
            .DefaultIfEmpty(0m)
            .Max());

        #endregion Private Properties

        #region Private Methods

        private string PolylinePoints(ChartSeries series, double plotWidth, double plotHeight, decimal max)
        {
            var points = series.Values
                .Take(Labels.Length)
                .Select((value, index) => (value, index))
                .Where(p => p.value.HasValue)
                .Select(p => $"{X(p.index, plotWidth).ToString(Inv)},{Y(p.value!.Value, plotHeight, max).ToString(Inv)}");

            return string.Join(" ", points);
        }

        /// <summary>Single values sit in the middle, otherwise the first and last label touch the edges.</summary>
        private double X(int index, double plotWidth) => Labels.Length == 1
            ? ChartLayout.Left + plotWidth / 2
            : ChartLayout.Left + plotWidth * index / (Labels.Length - 1);

        private static double Y(decimal value, double plotHeight, decimal max)
        {
            var fraction = max > 0 ? (double)Math.Clamp(value / max, 0, 1) : 0;
            return ChartLayout.Top + plotHeight * (1 - fraction);
        }

        #endregion Private Methods
    }
}
