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
#pragma warning disable IDE0007
        var plain = HtmlToPlainText(HtmlContent);
        if (string.IsNullOrWhiteSpace(plain))
        {
            return;
        }

        var centerX = X + Width / 2.0;

        string[] lines = plain.Split('\n');
        // Word often renders centred text high in the box — nudge down for optical vertical centre.
        var opticalDown = Math.Max(2.0, FontSizePx * OpticalVerticalBiasEm) + ExtraVerticalOffsetPx;
        double lineHeight = Math.Max(FontSizePx * 1.2, FontSizePx + 2);
        double startY = (Y + Height / 2.0) - ((lines.Length - 1) * lineHeight / 2.0) + opticalDown;

        builder.Append($"<text x=\"{Fmt(centerX)}\" y=\"{Fmt(startY)}\"");
        builder.Append(" text-anchor=\"middle\"");
        builder.Append($" font-family=\"Arial, sans-serif\" font-size=\"{Fmt(FontSizePx)}\" fill=\"#333333\"");
        CommonAttributes(builder);
        builder.Append('>');

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (i == 0)
            {
                builder.Append($"<tspan x=\"{Fmt(centerX)}\" dy=\"0\">{SvgText.EscapeXml(line)}</tspan>");
            }
            else
            {
                builder.Append($"<tspan x=\"{Fmt(centerX)}\" dy=\"{Fmt(lineHeight)}\">{SvgText.EscapeXml(line)}</tspan>");
            }
        }

        builder.Append("</text>");
#pragma warning restore IDE0007
    }

    static string HtmlToPlainText(string Html)
    {
        if (string.IsNullOrEmpty(Html))
        {
            return string.Empty;
        }

#pragma warning disable IDE0007
        string withBreaks = Regex.Replace(Html, "<\\s*br\\s*/?\\s*>", "\n", RegexOptions.IgnoreCase);
        var collapsed = Regex.Replace(withBreaks, "<[^>]+>", " ");
        string decoded = WebUtility.HtmlDecode(collapsed.Trim());
        decoded = decoded.Replace("\r\n", "\n", StringComparison.Ordinal);
        return decoded;
#pragma warning restore IDE0007
    }
}