using System;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;
using System.Text.RegularExpressions;

using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// An axis-aligned rectangle in render (SVG) coordinates.
/// </summary>
public readonly struct FlowchartSvgRect
{
    private readonly double _Left;
    private readonly double _Top;
    private readonly double _Right;
    private readonly double _Bottom;

    /// <summary>
    /// Creates a rectangle from its edges.
    /// </summary>
    public FlowchartSvgRect(double Left, double Top, double Right, double Bottom)
    {
        _Left = Math.Min(Left, Right);
        _Right = Math.Max(Left, Right);
        _Top = Math.Min(Top, Bottom);
        _Bottom = Math.Max(Top, Bottom);
    }

    /// <summary>
    /// Left edge.
    /// </summary>
    public double Left
    {
        get
        {
            return _Left;
        }
    }

    /// <summary>
    /// Top edge (smaller Y).
    /// </summary>
    public double Top
    {
        get
        {
            return _Top;
        }
    }

    /// <summary>
    /// Right edge.
    /// </summary>
    public double Right
    {
        get
        {
            return _Right;
        }
    }

    /// <summary>
    /// Bottom edge (larger Y).
    /// </summary>
    public double Bottom
    {
        get
        {
            return _Bottom;
        }
    }

    /// <summary>
    /// Returns true when the two rectangles overlap.
    /// </summary>
    public bool Intersects(FlowchartSvgRect Other)
    {
        return _Left < Other._Right && _Right > Other._Left && _Top < Other._Bottom && _Bottom > Other._Top;
    }

    /// <summary>
    /// Returns true when the point lies inside the rectangle.
    /// </summary>
    public bool Contains(double X, double Y)
    {
        return X > _Left && X < _Right && Y > _Top && Y < _Bottom;
    }
}

/// <summary>
/// Moves an edge label off its own connector so the text never sits on the line, the arrowhead or a node.
/// Works in render (SVG, Y down) coordinates.
/// Inspired by <c>EdgeLabelPlacement</c> in the adopted MSAGL slice: treat nodes, other strokes,
/// and the own connector as obstacles, then slide the label to the nearest clear offset.
/// </summary>
public static class FlowchartEdgeLabelClearanceHelper
{
    private static long _pointTests;
    private static long _nodeBoxTests;
    private static long _candidatePositions;

    internal static void ResetOperationCounts()
    {
        _pointTests = 0;
        _nodeBoxTests = 0;
        _candidatePositions = 0;
    }

    internal static void FlushOperationCounts()
    {
        FlowchartRenderContext.ReportOperationCount("label-point-tests", _pointTests % 4096);
        FlowchartRenderContext.ReportOperationCount("label-node-box-tests", _nodeBoxTests % 4096);
        FlowchartRenderContext.ReportOperationCount("label-candidate-positions", _candidatePositions % 4096);
    }

    private static void CountOperation(ref long Counter, string Name)
    {
        long Count = Interlocked.Increment(ref Counter);
        if ((Count & 4095) == 0) FlowchartRenderContext.ReportOperationCount(Name, 4096);
    }

    private const double LABEL_MARGIN_PX = 4.0;

    /// <summary>
    /// Wider side margin because Word and PNG fallback fonts often render text wider than the measured width.
    /// </summary>
    private const double LABEL_HORIZONTAL_MARGIN_PX = 8.0;

    /// <summary>
    /// A label further than this from its own connector reads as belonging to something else, so it is moved back.
    /// </summary>
    private const double MAX_DETACHED_LABEL_GAP_PX = 40.0;

    private const double SEARCH_STEP_PX = 2.0;

    private const double MAX_SEARCH_DISTANCE_PX = 90.0;

    private const double SAMPLE_SPACING_PX = 2.0;

    /// <summary>
    /// Step and limit for sliding a label along the flow when the only clear spot straight across is too far from its connector.
    /// </summary>
    private const double ALONG_FLOW_SHIFT_STEP_PX = 4.0;

    private const double MAX_ALONG_FLOW_SHIFT_PX = 60.0;

    private static readonly Regex _PathTokenPattern = new Regex(@"[MLC]|-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?", RegexOptions.Compiled);

    /// <summary>
    /// Returns a label centre that keeps the label box clear of the connector and of every node.
    /// The search moves across the flow: sideways for top-to-bottom diagrams and up or down for left-to-right diagrams,
    /// trying <paramref name="PreferredSign"/> first at each distance.
    /// </summary>
    /// <param name="CentreX">Proposed label centre X.</param>
    /// <param name="CentreY">Proposed label centre Y.</param>
    /// <param name="Width">Label width.</param>
    /// <param name="Height">Label height.</param>
    /// <param name="GuideSvgPoints">The connector guide points in render coordinates.</param>
    /// <param name="Direction">Diagram direction.</param>
    /// <param name="NodeRects">Every node box in render coordinates.</param>
    /// <param name="PreferredSign">+1 or -1 along the search axis.</param>
    /// <param name="OtherGuideSvgPoints">Guide points of every other edge in render coordinates. The label must not cover these either.</param>
    /// <returns>The adjusted centre, or the original when it is already clear or no clear spot is found.</returns>
    public static (double X, double Y) MoveClearOfConnector(
        double CentreX,
        double CentreY,
        double Width,
        double Height,
        IReadOnlyList<(double X, double Y)> GuideSvgPoints,
        FlowchartDirection Direction,
        IReadOnlyList<FlowchartSvgRect> NodeRects,
        double PreferredSign,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>>? OtherGuideSvgPoints = null) =>
        MoveClearOfConnector(CentreX, CentreY, Width, Height, GuideSvgPoints, Direction, NodeRects,
            PreferredSign, OtherGuideSvgPoints, null);

    internal static (double X, double Y) MoveClearOfConnector(
        double CentreX,
        double CentreY,
        double Width,
        double Height,
        IReadOnlyList<(double X, double Y)> GuideSvgPoints,
        FlowchartDirection Direction,
        IReadOnlyList<FlowchartSvgRect> NodeRects,
        double PreferredSign,
        IReadOnlyList<IReadOnlyList<(double X, double Y)>>? OtherGuideSvgPoints,
        FlowchartSvgSpatialIndex? ObstacleIndex)
    {
        if (GuideSvgPoints.Count < 2 || Width <= 0.0 || Height <= 0.0)
        {
            return (CentreX, CentreY);
        }

        List<(double X, double Y)> OwnSamples = SampleConnector(GuideSvgPoints);
        List<(double X, double Y)> Samples = new List<(double X, double Y)>(OwnSamples);
        if (OtherGuideSvgPoints is not null && ObstacleIndex == null)
        {
            foreach (IReadOnlyList<(double X, double Y)> OtherPoints in OtherGuideSvgPoints)
            {
                if (OtherPoints.Count >= 2)
                {
                    Samples.AddRange(SampleConnector(OtherPoints));
                }
            }
        }

        bool ClearOfOwnConnector = IsClear(CentreX, CentreY, Width, Height, OwnSamples, NodeRects, CheckNodes: false)
            && DistanceFromLabelToConnector(CentreX, CentreY, Width, Height, OwnSamples) <= MAX_DETACHED_LABEL_GAP_PX;
        bool HasOtherConnectors = ObstacleIndex?.PointCount > 0 || Samples.Count > OwnSamples.Count;
        if (ClearOfOwnConnector && (!HasOtherConnectors || IsClear(CentreX, CentreY, Width, Height, Samples, NodeRects, CheckNodes: false, ObstacleIndex)))
        {
            return (CentreX, CentreY);
        }

        if (HasOtherConnectors && TrySearchClearCentre(OwnSamples, Samples, Width, Height, Direction, NodeRects, PreferredSign, ObstacleIndex, out double ClearX, out double ClearY))
        {
            return (ClearX, ClearY);
        }

        if (ClearOfOwnConnector)
        {
            return (CentreX, CentreY);
        }

        if (TrySearchClearCentre(OwnSamples, OwnSamples, Width, Height, Direction, NodeRects, PreferredSign, null, out ClearX, out ClearY))
        {
            return (ClearX, ClearY);
        }

        return ResolveArcLengthMidpoint(OwnSamples);
    }

    /// <summary>
    /// Searches across the flow from the arc-length midpoint of the label's own connector for a spot clear of <paramref name="ObstacleSamples"/> and every node.
    /// </summary>
    private static bool TrySearchClearCentre(
        IReadOnlyList<(double X, double Y)> OwnSamples,
        IReadOnlyList<(double X, double Y)> ObstacleSamples,
        double Width,
        double Height,
        FlowchartDirection Direction,
        IReadOnlyList<FlowchartSvgRect> NodeRects,
        double PreferredSign,
        FlowchartSvgSpatialIndex? ObstacleIndex,
        out double ClearX,
        out double ClearY)
    {
        (double CentreX, double CentreY) = ResolveArcLengthMidpoint(OwnSamples);
        bool SearchAlongX = Direction == FlowchartDirection.TopToBottom || Direction == FlowchartDirection.BottomToTop;
        double FirstSign = PreferredSign < 0.0 ? -1.0 : 1.0;
        if (TrySearchAcrossFlow(CentreX, CentreY, 0.0, SearchAlongX, FirstSign, Width, Height, ObstacleSamples, NodeRects, ObstacleIndex, out ClearX, out ClearY) && DistanceFromLabelToConnector(ClearX, ClearY, Width, Height, OwnSamples) <= MAX_DETACHED_LABEL_GAP_PX)
        {
            return true;
        }

        double FirstClearX = ClearX;
        double FirstClearY = ClearY;
        bool FoundFirst = IsClear(FirstClearX, FirstClearY, Width, Height, ObstacleSamples, NodeRects, CheckNodes: true, ObstacleIndex);
        for (double Shift = ALONG_FLOW_SHIFT_STEP_PX; Shift <= MAX_ALONG_FLOW_SHIFT_PX; Shift += ALONG_FLOW_SHIFT_STEP_PX)
        {
            for (int ShiftAttempt = 0; ShiftAttempt < 2; ShiftAttempt++)
            {
                double SignedShift = ShiftAttempt == 0 ? Shift : -Shift;
                if (TrySearchAcrossFlow(CentreX, CentreY, SignedShift, SearchAlongX, FirstSign, Width, Height, ObstacleSamples, NodeRects, ObstacleIndex, out double ShiftedX, out double ShiftedY) && DistanceFromLabelToConnector(ShiftedX, ShiftedY, Width, Height, OwnSamples) <= MAX_DETACHED_LABEL_GAP_PX)
                {
                    ClearX = ShiftedX;
                    ClearY = ShiftedY;
                    return true;
                }
            }
        }

        ClearX = FirstClearX;
        ClearY = FirstClearY;
        return FoundFirst;
    }

    /// <summary>
    /// Walks across the flow from a point that sits <paramref name="AlongFlowShift"/> along the flow from the connector midpoint,
    /// returning the first label centre clear of <paramref name="ObstacleSamples"/> and every node.
    /// </summary>
    private static bool TrySearchAcrossFlow(
        double CentreX,
        double CentreY,
        double AlongFlowShift,
        bool SearchAlongX,
        double FirstSign,
        double Width,
        double Height,
        IReadOnlyList<(double X, double Y)> ObstacleSamples,
        IReadOnlyList<FlowchartSvgRect> NodeRects,
        FlowchartSvgSpatialIndex? ObstacleIndex,
        out double ClearX,
        out double ClearY)
    {
        double StartX = SearchAlongX ? CentreX : CentreX + AlongFlowShift;
        double StartY = SearchAlongX ? CentreY + AlongFlowShift : CentreY;
        for (double Distance = SEARCH_STEP_PX; Distance <= MAX_SEARCH_DISTANCE_PX; Distance += SEARCH_STEP_PX)
        {
            for (int Attempt = 0; Attempt < 2; Attempt++)
            {
                CountOperation(ref _candidatePositions, "label-candidate-positions");
                double Sign = Attempt == 0 ? FirstSign : -FirstSign;
                double CandidateX = SearchAlongX ? StartX + (Sign * Distance) : StartX;
                double CandidateY = SearchAlongX ? StartY : StartY + (Sign * Distance);
                if (IsClear(CandidateX, CandidateY, Width, Height, ObstacleSamples, NodeRects, CheckNodes: true, ObstacleIndex))
                {
                    ClearX = CandidateX;
                    ClearY = CandidateY;
                    return true;
                }
            }
        }

        ClearX = StartX;
        ClearY = StartY;
        return false;
    }

    private static (double X, double Y) ResolveArcLengthMidpoint(IReadOnlyList<(double X, double Y)> Samples)
    {
        double TotalLength = 0.0;
        for (int SampleIndex = 1; SampleIndex < Samples.Count; SampleIndex++)
        {
            TotalLength += Distance(Samples[SampleIndex - 1], Samples[SampleIndex]);
        }

        double Remaining = TotalLength / 2.0;
        for (int SampleIndex = 1; SampleIndex < Samples.Count; SampleIndex++)
        {
            double SegmentLength = Distance(Samples[SampleIndex - 1], Samples[SampleIndex]);
            if (SegmentLength >= Remaining && SegmentLength > 1e-9)
            {
                double Ratio = Remaining / SegmentLength;
                return (
                    Samples[SampleIndex - 1].X + ((Samples[SampleIndex].X - Samples[SampleIndex - 1].X) * Ratio),
                    Samples[SampleIndex - 1].Y + ((Samples[SampleIndex].Y - Samples[SampleIndex - 1].Y) * Ratio));
            }

            Remaining -= SegmentLength;
        }

        return Samples[Samples.Count / 2];
    }

    private static double DistanceFromLabelToConnector(double CentreX, double CentreY, double Width, double Height, IReadOnlyList<(double X, double Y)> Samples)
    {
        double Nearest = double.MaxValue;
        foreach ((double X, double Y) Sample in Samples)
        {
            double DeltaX = Math.Max(0.0, Math.Abs(Sample.X - CentreX) - (Width / 2.0));
            double DeltaY = Math.Max(0.0, Math.Abs(Sample.Y - CentreY) - (Height / 2.0));
            double Gap = Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
            if (Gap < Nearest)
            {
                Nearest = Gap;
            }
        }

        return Nearest;
    }

    private static double Distance((double X, double Y) Start, (double X, double Y) End)
    {
        return Math.Sqrt(((End.X - Start.X) * (End.X - Start.X)) + ((End.Y - Start.Y) * (End.Y - Start.Y)));
    }

    private static bool IsClear(
        double CentreX,
        double CentreY,
        double Width,
        double Height,
        IReadOnlyList<(double X, double Y)> Samples,
        IReadOnlyList<FlowchartSvgRect> NodeRects,
        bool CheckNodes,
        FlowchartSvgSpatialIndex? ObstacleIndex = null)
    {
        double HalfWidth = (Width / 2.0) + LABEL_HORIZONTAL_MARGIN_PX;
        double HalfHeight = (Height / 2.0) + LABEL_MARGIN_PX;
        FlowchartSvgRect LabelRect = new FlowchartSvgRect(CentreX - HalfWidth, CentreY - HalfHeight, CentreX + HalfWidth, CentreY + HalfHeight);
        if (ObstacleIndex?.ContainsPoint(LabelRect) == true) return false;
        foreach ((double X, double Y) Sample in Samples)
        {
            CountOperation(ref _pointTests, "label-point-tests");
            if (LabelRect.Contains(Sample.X, Sample.Y))
            {
                return false;
            }
        }

        if (!CheckNodes)
        {
            return true;
        }

        double NodeHalfWidth = (Width / 2.0) + LABEL_MARGIN_PX;
        FlowchartSvgRect NodeCheckRect = new FlowchartSvgRect(CentreX - NodeHalfWidth, CentreY - HalfHeight, CentreX + NodeHalfWidth, CentreY + HalfHeight);
        if (ObstacleIndex?.IntersectsRectangle(NodeCheckRect) == true) return false;
        foreach (FlowchartSvgRect NodeRect in NodeRects)
        {
            CountOperation(ref _nodeBoxTests, "label-node-box-tests");
            if (NodeCheckRect.Intersects(NodeRect))
            {
                return false;
            }
        }

        return true;
    }

    internal static List<(double X, double Y)> SampleConnector(IReadOnlyList<(double X, double Y)> GuideSvgPoints)
    {
        List<(double X, double Y)> Samples = [];
        if (FlowchartCurvePathBuilder.TryBuildStubBridgeCurvePathData(GuideSvgPoints, out string PathData)
            && TrySamplePathData(PathData, Samples))
        {
            return Samples;
        }

        Samples.Clear();
        for (int PointIndex = 1; PointIndex < GuideSvgPoints.Count; PointIndex++)
        {
            AppendLineSamples(Samples, GuideSvgPoints[PointIndex - 1], GuideSvgPoints[PointIndex]);
        }

        return Samples;
    }

    private static bool TrySamplePathData(string PathData, List<(double X, double Y)> Samples)
    {
        MatchCollection Tokens = _PathTokenPattern.Matches(PathData);
        (double X, double Y) Current = (0.0, 0.0);
        int TokenIndex = 0;
        while (TokenIndex < Tokens.Count)
        {
            string Command = Tokens[TokenIndex].Value;
            TokenIndex++;
            if (Command == "M" || Command == "L")
            {
                if (TokenIndex + 1 >= Tokens.Count)
                {
                    return false;
                }

                (double X, double Y) Next = (ParseNumber(Tokens[TokenIndex].Value), ParseNumber(Tokens[TokenIndex + 1].Value));
                TokenIndex += 2;
                if (Command == "L")
                {
                    AppendLineSamples(Samples, Current, Next);
                }

                Current = Next;
            }
            else if (Command == "C")
            {
                if (TokenIndex + 5 >= Tokens.Count)
                {
                    return false;
                }

                (double X, double Y) Control1 = (ParseNumber(Tokens[TokenIndex].Value), ParseNumber(Tokens[TokenIndex + 1].Value));
                (double X, double Y) Control2 = (ParseNumber(Tokens[TokenIndex + 2].Value), ParseNumber(Tokens[TokenIndex + 3].Value));
                (double X, double Y) End = (ParseNumber(Tokens[TokenIndex + 4].Value), ParseNumber(Tokens[TokenIndex + 5].Value));
                TokenIndex += 6;
                AppendCubicSamples(Samples, Current, Control1, Control2, End);
                Current = End;
            }
            else
            {
                return false;
            }
        }

        return Samples.Count > 0;
    }

    private static double ParseNumber(string Text)
    {
        return double.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private static void AppendLineSamples(List<(double X, double Y)> Samples, (double X, double Y) Start, (double X, double Y) End)
    {
        double Length = Math.Sqrt(((End.X - Start.X) * (End.X - Start.X)) + ((End.Y - Start.Y) * (End.Y - Start.Y)));
        int Steps = Math.Max(1, (int)Math.Ceiling(Length / SAMPLE_SPACING_PX));
        for (int Step = 0; Step <= Steps; Step++)
        {
            double T = (double)Step / Steps;
            Samples.Add((Start.X + ((End.X - Start.X) * T), Start.Y + ((End.Y - Start.Y) * T)));
        }
    }

    private static void AppendCubicSamples(
        List<(double X, double Y)> Samples,
        (double X, double Y) Start,
        (double X, double Y) Control1,
        (double X, double Y) Control2,
        (double X, double Y) End)
    {
        const int CUBIC_SAMPLE_COUNT = 64;
        for (int Step = 0; Step <= CUBIC_SAMPLE_COUNT; Step++)
        {
            double T = (double)Step / CUBIC_SAMPLE_COUNT;
            double U = 1.0 - T;
            double A = U * U * U;
            double B = 3.0 * U * U * T;
            double C = 3.0 * U * T * T;
            double D = T * T * T;
            Samples.Add((
                (A * Start.X) + (B * Control1.X) + (C * Control2.X) + (D * End.X),
                (A * Start.Y) + (B * Control1.Y) + (C * Control2.Y) + (D * End.Y)));
        }
    }
}
