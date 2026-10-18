using System;
using System.Linq;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Reserves frame title text before fitting, using the existing renderer's text advances, line height and clearance.</summary>
internal static class FlowchartFrameTitleReservation
{
    /// <summary>Immutable measured title geometry; padding around the frame remains the frame builder's responsibility.</summary>
    /// <param name="TextWidthPx">Widest explicit title line under the renderer's existing text metric.</param>
    /// <param name="BandHeightPx">Complete title band rounded outward to the existing layout grid.</param>
    internal readonly record struct Measurement(double TextWidthPx, int BandHeightPx);

    /// <summary>Measures every explicit line before frame fitting; routing uses the same reserved band instead of growing the frame later.</summary>
    /// <param name="Title">Resolved group title, including explicit line breaks.</param>
    /// <param name="FontSizePx">Existing diagram title font metric.</param>
    /// <returns>The width and reserved band used by member-backed, empty and nested frames.</returns>
    internal static Measurement Measure(string Title, double FontSizePx)
    {
        string[] Lines = Title.Split('\n');
        return new(Lines.Max(Line => FlowchartRichLabelSvgWriter.EstimateLineWidthPx(Line, FontSizePx)),
            BandHeight(FontSizePx, Lines.Length));
    }

    /// <summary>Returns the same reserved strip for routing without repeating width measurement per route.</summary>
    /// <param name="FontSizePx">Existing renderer font metric.</param>
    /// <param name="LineCount">Explicit title lines.</param>
    /// <returns>The renderer's complete text/clearance band, rounded outward once.</returns>
    internal static int BandHeight(double FontSizePx, int LineCount) => DiagramGridMetrics.CeilToGrid(Math.Max(
        DiagramGridMetrics.GRID_UNIT_PX, FlowchartSubgraphSvgRenderer.RequiredTitleBandPx(FontSizePx, LineCount)));
}
