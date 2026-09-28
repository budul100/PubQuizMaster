namespace PubQuizMaster.Web.Components.Charts
{
    /// <summary>
    /// Shared geometry of the SVG charts. The charts scale with their container,
    /// all coordinates are in viewBox units.
    /// </summary>
    public static class ChartLayout
    {
        #region Public Fields

        public const double Bottom = 28;

        public const double Left = 44;

        public const double Right = 12;

        public const double Top = 12;

        public const double Width = 640;

        #endregion Public Fields

        #region Public Methods

        /// <summary>Every n-th x label, so at most about twelve labels are drawn.</summary>
        public static int LabelStep(int count) => Math.Max(1, (int)Math.Ceiling(count / 12.0));

        /// <summary>Round upper bound for the y axis, e.g. 37 becomes 40.</summary>
        public static decimal NiceMax(decimal value)
        {
            if (value <= 0) return 1m;

            var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)value)));
            var steps = new[] { 1m, 1.5m, 2m, 2.5m, 3m, 4m, 5m, 6m, 8m, 10m };

            return steps.Select(s => s * magnitude).First(s => s >= value);
        }

        #endregion Public Methods
    }
}
