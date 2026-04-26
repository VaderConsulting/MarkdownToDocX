using System.Net;
using System.Text.RegularExpressions;

namespace MermaidSharp.Rendering;

/// <summary>
/// Renders node and edge labels as native SVG &lt;text&gt; (not HTML foreignObject) so output works in
/// consumers that do not support foreignObject well (for example Microsoft Word embedded SVG).
/// Original Naiad used xhtml foreignObject for browser parity; see UpstreamContributions.md in this tree.
/// </summary>
public class SvgForeignObject : SvgElement
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public required string HtmlContent { get; set; }

    /// <summary>
    /// Font size for emitted SVG (should match layout measurement). Defaults to legacy 16px.
    /// </summary>
    public double FontSizePx { get; set; } = 16;

    /// <summary>
    /// Scales the optical vertical nudge (em). Browsers and Word differ; flowcharts can use a slightly lower em than other diagram types.
    /// </summary>
    public double OpticalVerticalBiasEm { get; set; } = 0.12;

    /// <summary>
    /// Additional downward offset in SVG user units (typically px) after the em-based nudge—for example Word vs reference vertical centring.
    /// </summary>
    public double ExtraVerticalOffsetPx { get; set; }

    public override void ToXml(StringBuilder builder)
    {
        var plain = HtmlToPlainText(HtmlContent);
        if (string.IsNullOrWhiteSpace(plain))
        {
            return;
        }

        var centerX = X + Width / 2.0;
        // Word often renders dominant-baseline:middle high in the box — nudge down for optical vertical centre (same heuristic as StateRenderer normal states).
        var opticalDown = Math.Max(2.0, FontSizePx * OpticalVerticalBiasEm);
        var centerY = Y + Height / 2.0 + opticalDown + ExtraVerticalOffsetPx;
        builder.Append($"<text x=\"{Fmt(centerX)}\" y=\"{Fmt(centerY)}\"");
        builder.Append(" text-anchor=\"middle\" dominant-baseline=\"middle\"");
        builder.Append($" font-family=\"Arial, sans-serif\" font-size=\"{Fmt(FontSizePx)}\" fill=\"#333333\"");
        CommonAttributes(builder);
        builder.Append('>');
        builder.Append(SvgText.EscapeXml(plain));
        builder.Append("</text>");
    }

    static string HtmlToPlainText(string Html)
    {
        if (string.IsNullOrEmpty(Html))
        {
            return string.Empty;
        }

        var collapsed = Regex.Replace(Html, "<[^>]+>", " ");
        return WebUtility.HtmlDecode(collapsed.Trim());
    }
}