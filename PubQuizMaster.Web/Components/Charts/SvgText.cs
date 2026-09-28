using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace PubQuizMaster.Web.Components.Charts
{
    /// <summary>
    /// SVG text element for the charts. Razor reserves the text tag for its own markup transitions
    /// and rejects attributes on it, so the element is built in code instead of markup.
    /// </summary>
    public class SvgText : ComponentBase
    {
        #region Private Fields

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        #endregion Private Fields

        #region Public Properties

        /// <summary>SVG text-anchor: start, middle or end.</summary>
        [Parameter] public string Anchor { get; set; } = "start";

        [Parameter] public string Fill { get; set; } = "var(--bs-secondary-color)";

        [Parameter] public double FontSize { get; set; } = 11;

        [Parameter] public string? Text { get; set; }

        [Parameter] public double X { get; set; }

        [Parameter] public double Y { get; set; }

        #endregion Public Properties

        #region Protected Methods

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "text");
            builder.AddAttribute(1, "x", X.ToString(Inv));
            builder.AddAttribute(2, "y", Y.ToString(Inv));
            builder.AddAttribute(3, "text-anchor", Anchor);
            builder.AddAttribute(4, "font-size", FontSize.ToString(Inv));
            builder.AddAttribute(5, "fill", Fill);
            builder.AddContent(6, Text);
            builder.CloseElement();
        }

        #endregion Protected Methods
    }
}
