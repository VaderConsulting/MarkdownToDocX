namespace MermaidSharp.Rendering;

#pragma warning disable IDE0007

/// <summary>
/// Mermaid default edge stroke widths and dash patterns from
/// <c>packages/mermaid/src/styles.ts</c> (<c>edge-thickness-*</c>, <c>edge-pattern-*</c>).
/// </summary>
public static class MermaidEdgeStroke
{
    /// <summary>Matches <c>.edge-thickness-normal</c> (theme <c>strokeWidth</c> default 1).</summary>
    public const double NORMAL_STROKE_WIDTH_PX = 1.0;

    /// <summary>Matches <c>.edge-thickness-thick</c>.</summary>
    public const double THICK_STROKE_WIDTH_PX = 3.5;

    /// <summary>Matches <c>.edge-pattern-dotted</c> (<c>stroke-dasharray: 2</c>).</summary>
    public const string DOTTED_DASHARRAY = "2";

    /// <summary>Matches <c>.edge-pattern-dashed</c> (<c>stroke-dasharray: 3</c>).</summary>
    public const string DASHED_DASHARRAY = "3";

    /// <summary>
    /// Resolves stroke width: explicit style override, then thick line style, then theme/default.
    /// </summary>
    public static double ResolveStrokeWidth(
        MermaidSharp.Models.Style? style,
        MermaidSharp.Models.EdgeStyle lineStyle,
        double? themeStrokeWidthPx = null)
    {
        if (style is not null && style.StrokeWidth.HasValue)
        {
            return style.StrokeWidth.Value;
        }

        if (lineStyle == MermaidSharp.Models.EdgeStyle.Thick)
        {
            return THICK_STROKE_WIDTH_PX;
        }

        if (themeStrokeWidthPx.HasValue)
        {
            return themeStrokeWidthPx.Value;
        }

        return NORMAL_STROKE_WIDTH_PX;
    }

    /// <summary>
    /// Resolves dash array: explicit style override, then line style pattern.
    /// </summary>
    public static string? ResolveStrokeDasharray(
        MermaidSharp.Models.Style? style,
        MermaidSharp.Models.EdgeStyle lineStyle)
    {
        if (style is not null && style.StrokeDasharray is not null)
        {
            return style.StrokeDasharray;
        }

        if (lineStyle == MermaidSharp.Models.EdgeStyle.Dotted)
        {
            return DOTTED_DASHARRAY;
        }

        return null;
    }
}

#pragma warning restore IDE0007
