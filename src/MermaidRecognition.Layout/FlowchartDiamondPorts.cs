using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>
/// Which slanted side of a decision diamond a port sits on.
/// Vertices, where two sides meet, use <see cref="FlowchartDiamondEdge.None"/>.
/// </summary>
public enum FlowchartDiamondEdge
{
    /// <summary>
    /// A vertex, not a point part-way along a side.
    /// </summary>
    None = 0,

    /// <summary>
    /// The side from the top vertex to the right vertex.
    /// </summary>
    TopRight = 1,

    /// <summary>
    /// The side from the right vertex to the bottom vertex.
    /// </summary>
    BottomRight = 2,

    /// <summary>
    /// The side from the bottom vertex to the left vertex.
    /// </summary>
    BottomLeft = 3,

    /// <summary>
    /// The side from the left vertex to the top vertex.
    /// </summary>
    TopLeft = 4
}

/// <summary>
/// One whole-pixel port on a decision diamond, relative to the diamond centre.
/// </summary>
public sealed class FlowchartDiamondPort
{
    /// <summary>
    /// Creates a port.
    /// </summary>
    /// <param name="OffsetX">Horizontal offset from the centre, in pixels.</param>
    /// <param name="OffsetY">Vertical offset from the centre, in pixels.</param>
    /// <param name="IsVertex">True when the port is a vertex.</param>
    /// <param name="Edge">The slanted side, or <see cref="FlowchartDiamondEdge.None"/> for a vertex.</param>
    /// <param name="DepartureX">Horizontal leaving direction, -1, 0, or 1.</param>
    /// <param name="DepartureY">Vertical leaving direction, -1, 0, or 1.</param>
    public FlowchartDiamondPort(
        int OffsetX,
        int OffsetY,
        bool IsVertex,
        FlowchartDiamondEdge Edge,
        int DepartureX,
        int DepartureY)
    {
        _OffsetX = OffsetX;
        _OffsetY = OffsetY;
        _IsVertex = IsVertex;
        _Edge = Edge;
        _DepartureX = DepartureX;
        _DepartureY = DepartureY;
    }

    private readonly int _OffsetX;

    private readonly int _OffsetY;

    private readonly bool _IsVertex;

    private readonly FlowchartDiamondEdge _Edge;

    private readonly int _DepartureX;

    private readonly int _DepartureY;

    /// <summary>
    /// Horizontal offset from the centre, in whole pixels.
    /// </summary>
    public int OffsetX
    {
        get
        {
            return _OffsetX;
        }
    }

    /// <summary>
    /// Vertical offset from the centre, in whole pixels. Positive is up.
    /// </summary>
    public int OffsetY
    {
        get
        {
            return _OffsetY;
        }
    }

    /// <summary>
    /// True when this port is one of the four vertices.
    /// </summary>
    public bool IsVertex
    {
        get
        {
            return _IsVertex;
        }
    }

    /// <summary>
    /// The slanted side this port belongs to.
    /// </summary>
    public FlowchartDiamondEdge Edge
    {
        get
        {
            return _Edge;
        }
    }

    /// <summary>
    /// Horizontal part of the leaving direction. The line leaves horizontally or vertically.
    /// </summary>
    public int DepartureX
    {
        get
        {
            return _DepartureX;
        }
    }

    /// <summary>
    /// Vertical part of the leaving direction.
    /// </summary>
    public int DepartureY
    {
        get
        {
            return _DepartureY;
        }
    }
}

/// <summary>
/// Ports on a decision diamond. Vertices are used first. Each slanted edge then
/// receives only as many evenly spaced ports as the lines on that edge need.
/// </summary>
public static class FlowchartDiamondPorts
{
    private const int MAXIMUM_EDGE_PORTS = 4;

    /// <summary>
    /// The four vertices of the standard cell, rounded to whole pixels about the centre.
    /// Order is top, right, bottom, left.
    /// </summary>
    /// <param name="WidthPx">Diamond cell width.</param>
    /// <param name="HeightPx">Diamond cell height.</param>
    /// <returns>Four vertex offsets.</returns>
    public static IReadOnlyList<(int X, int Y)> Vertices(int WidthPx, int HeightPx)
    {
        int HalfWidth = RoundSymmetric(WidthPx / 2.0);
        int HalfHeight = RoundSymmetric(HeightPx / 2.0);
        return
        [
            (0, HalfHeight),
            (HalfWidth, 0),
            (0, -HalfHeight),
            (-HalfWidth, 0)
        ];
    }

    /// <summary>
    /// Evenly spaced ports along one slanted edge, excluding the vertices.
    /// One port is the midpoint. Two ports are the one-third points. Three are the
    /// quarter points. Four are the one-fifth points.
    /// </summary>
    /// <param name="WidthPx">Diamond cell width.</param>
    /// <param name="HeightPx">Diamond cell height.</param>
    /// <param name="Edge">Which slanted edge.</param>
    /// <param name="PortCount">How many ports that edge needs, from 1 to 4.</param>
    /// <param name="Ports">Ports ordered from the edge start toward the edge end.</param>
    /// <returns>False when the count is outside 1 to 4 or the edge is a vertex.</returns>
    public static bool TryGetEdgePorts(
        int WidthPx,
        int HeightPx,
        FlowchartDiamondEdge Edge,
        int PortCount,
        out IReadOnlyList<(int X, int Y)> Ports)
    {
        Ports = [];
        if (PortCount < 1 || PortCount > MAXIMUM_EDGE_PORTS || Edge == FlowchartDiamondEdge.None)
        {
            return false;
        }

        if (!TryGetEdgeEnds(WidthPx, HeightPx, Edge, out double StartX, out double StartY, out double EndX, out double EndY))
        {
            return false;
        }

        List<(int X, int Y)> Built = new List<(int X, int Y)>();
        for (int Index = 1; Index <= PortCount; Index++)
        {
            double Fraction = Index / (double)(PortCount + 1);
            double X = StartX + ((EndX - StartX) * Fraction);
            double Y = StartY + ((EndY - StartY) * Fraction);
            Built.Add((RoundSymmetric(X), RoundSymmetric(Y)));
        }

        Ports = Built;
        return true;
    }

    /// <summary>
    /// Assigns a port to each destination. Vertices are filled first. An edge gains
    /// ports only for the lines that could not take a vertex, and only on that edge.
    /// </summary>
    /// <param name="WidthPx">Diamond cell width.</param>
    /// <param name="HeightPx">Diamond cell height.</param>
    /// <param name="Destinations">Other-node positions relative to the diamond centre.</param>
    /// <param name="Ports">One port per destination, in the same order.</param>
    /// <returns>False when one edge would need more than four ports.</returns>
    public static bool TryAssign(
        int WidthPx,
        int HeightPx,
        IReadOnlyList<(double X, double Y)> Destinations,
        out IReadOnlyList<FlowchartDiamondPort> Ports)
    {
        Ports = [];
        if (Destinations.Count == 0)
        {
            return true;
        }

        int[] Claim = new int[Destinations.Count];
        int[] Winner = [-1, -1, -1, -1];
        for (int Index = 0; Index < Destinations.Count; Index++)
        {
            Claim[Index] = (int)ClaimVertex(Destinations[Index].X, Destinations[Index].Y);
            int Vertex = Claim[Index];
            if (Winner[Vertex] < 0 || IsCloserToAxis(Destinations[Index], Destinations[Winner[Vertex]], (VertexKind)Vertex))
            {
                Winner[Vertex] = Index;
            }
        }

        List<int>[] EdgeLines = new List<int>[4];
        for (int EdgeIndex = 0; EdgeIndex < EdgeLines.Length; EdgeIndex++)
        {
            EdgeLines[EdgeIndex] = new List<int>();
        }

        FlowchartDiamondPort[] Assigned = new FlowchartDiamondPort[Destinations.Count];
        IReadOnlyList<(int X, int Y)> VertexPorts = Vertices(WidthPx, HeightPx);
        for (int Index = 0; Index < Destinations.Count; Index++)
        {
            int Vertex = Claim[Index];
            if (Winner[Vertex] == Index)
            {
                (int X, int Y) = VertexPorts[Vertex];
                (int DepartureX, int DepartureY) = VertexDeparture((VertexKind)Vertex);
                Assigned[Index] = new FlowchartDiamondPort(
                    X,
                    Y,
                    true,
                    FlowchartDiamondEdge.None,
                    DepartureX,
                    DepartureY);
                continue;
            }

            FlowchartDiamondEdge Edge = EdgeForRejectedVertex(
                (VertexKind)Vertex,
                Destinations[Index].X,
                Destinations[Index].Y);
            EdgeLines[(int)Edge - 1].Add(Index);
        }

        for (int EdgeIndex = 0; EdgeIndex < EdgeLines.Length; EdgeIndex++)
        {
            List<int> Lines = EdgeLines[EdgeIndex];
            if (Lines.Count == 0)
            {
                continue;
            }

            if (Lines.Count > MAXIMUM_EDGE_PORTS)
            {
                return false;
            }

            FlowchartDiamondEdge Edge = (FlowchartDiamondEdge)(EdgeIndex + 1);
            if (!TryGetEdgePorts(WidthPx, HeightPx, Edge, Lines.Count, out IReadOnlyList<(int X, int Y)> EdgePorts))
            {
                return false;
            }

            if (!TryGetEdgeEnds(WidthPx, HeightPx, Edge, out double StartX, out double StartY, out double EndX, out double EndY))
            {
                return false;
            }

            Lines.Sort(delegate(int Left, int Right)
            {
                double LeftT = ParameterAlongEdge(Destinations[Left], StartX, StartY, EndX, EndY);
                double RightT = ParameterAlongEdge(Destinations[Right], StartX, StartY, EndX, EndY);
                return LeftT.CompareTo(RightT);
            });

            for (int PortIndex = 0; PortIndex < Lines.Count; PortIndex++)
            {
                int LineIndex = Lines[PortIndex];
                (int X, int Y) = EdgePorts[PortIndex];
                (int DepartureX, int DepartureY) = EdgeDeparture(
                    Edge,
                    Destinations[LineIndex].X,
                    Destinations[LineIndex].Y);
                Assigned[LineIndex] = new FlowchartDiamondPort(
                    X,
                    Y,
                    false,
                    Edge,
                    DepartureX,
                    DepartureY);
            }
        }

        Ports = Assigned;
        return true;
    }

    /// <summary>
    /// Rounds a port offset to a whole pixel, away from zero, so a mirror keeps the opposite sign.
    /// </summary>
    /// <param name="Value">Offset from the centre.</param>
    /// <returns>The nearest whole pixel.</returns>
    public static int RoundSymmetric(double Value)
    {
        return (int)Math.Round(Value, MidpointRounding.AwayFromZero);
    }

    private static bool TryGetEdgeEnds(
        int WidthPx,
        int HeightPx,
        FlowchartDiamondEdge Edge,
        out double StartX,
        out double StartY,
        out double EndX,
        out double EndY)
    {
        double HalfWidth = WidthPx / 2.0;
        double HalfHeight = HeightPx / 2.0;
        switch (Edge)
        {
            case FlowchartDiamondEdge.TopRight:
                StartX = 0.0;
                StartY = HalfHeight;
                EndX = HalfWidth;
                EndY = 0.0;
                return true;
            case FlowchartDiamondEdge.BottomRight:
                StartX = HalfWidth;
                StartY = 0.0;
                EndX = 0.0;
                EndY = -HalfHeight;
                return true;
            case FlowchartDiamondEdge.BottomLeft:
                StartX = 0.0;
                StartY = -HalfHeight;
                EndX = -HalfWidth;
                EndY = 0.0;
                return true;
            case FlowchartDiamondEdge.TopLeft:
                StartX = -HalfWidth;
                StartY = 0.0;
                EndX = 0.0;
                EndY = HalfHeight;
                return true;
            default:
                StartX = 0.0;
                StartY = 0.0;
                EndX = 0.0;
                EndY = 0.0;
                return false;
        }
    }

    private static VertexKind ClaimVertex(double X, double Y)
    {
        if (Math.Abs(Y) >= Math.Abs(X))
        {
            if (Y >= 0.0)
            {
                return VertexKind.Top;
            }

            return VertexKind.Bottom;
        }

        if (X >= 0.0)
        {
            return VertexKind.Right;
        }

        return VertexKind.Left;
    }

    private static bool IsCloserToAxis(
        (double X, double Y) Candidate,
        (double X, double Y) Incumbent,
        VertexKind Vertex)
    {
        double CandidateOffset = AxisOffset(Candidate, Vertex);
        double IncumbentOffset = AxisOffset(Incumbent, Vertex);
        return CandidateOffset < IncumbentOffset;
    }

    private static double AxisOffset((double X, double Y) Point, VertexKind Vertex)
    {
        if (Vertex == VertexKind.Top || Vertex == VertexKind.Bottom)
        {
            return Math.Abs(Point.X);
        }

        return Math.Abs(Point.Y);
    }

    private static FlowchartDiamondEdge EdgeForRejectedVertex(VertexKind Vertex, double X, double Y)
    {
        switch (Vertex)
        {
            case VertexKind.Top:
                if (X >= 0.0)
                {
                    return FlowchartDiamondEdge.TopRight;
                }

                return FlowchartDiamondEdge.TopLeft;
            case VertexKind.Bottom:
                if (X >= 0.0)
                {
                    return FlowchartDiamondEdge.BottomRight;
                }

                return FlowchartDiamondEdge.BottomLeft;
            case VertexKind.Right:
                if (Y >= 0.0)
                {
                    return FlowchartDiamondEdge.TopRight;
                }

                return FlowchartDiamondEdge.BottomRight;
            default:
                if (Y >= 0.0)
                {
                    return FlowchartDiamondEdge.TopLeft;
                }

                return FlowchartDiamondEdge.BottomLeft;
        }
    }

    private static (int X, int Y) VertexDeparture(VertexKind Vertex)
    {
        switch (Vertex)
        {
            case VertexKind.Top:
                return (0, 1);
            case VertexKind.Right:
                return (1, 0);
            case VertexKind.Bottom:
                return (0, -1);
            default:
                return (-1, 0);
        }
    }

    private static (int X, int Y) EdgeDeparture(FlowchartDiamondEdge Edge, double DestinationX, double DestinationY)
    {
        int HorizontalX = Edge == FlowchartDiamondEdge.TopRight || Edge == FlowchartDiamondEdge.BottomRight
            ? 1
            : -1;
        int VerticalY = Edge == FlowchartDiamondEdge.TopRight || Edge == FlowchartDiamondEdge.TopLeft
            ? 1
            : -1;
        double HorizontalScore = DestinationX * HorizontalX;
        double VerticalScore = DestinationY * VerticalY;
        if (VerticalScore > HorizontalScore)
        {
            return (0, VerticalY);
        }

        if (HorizontalScore > VerticalScore)
        {
            return (HorizontalX, 0);
        }

        if (Math.Abs(DestinationY) >= Math.Abs(DestinationX))
        {
            return (0, VerticalY);
        }

        return (HorizontalX, 0);
    }

    private static double ParameterAlongEdge(
        (double X, double Y) Point,
        double StartX,
        double StartY,
        double EndX,
        double EndY)
    {
        double SpanX = EndX - StartX;
        double SpanY = EndY - StartY;
        double LengthSquared = (SpanX * SpanX) + (SpanY * SpanY);
        if (LengthSquared <= 0.0)
        {
            return 0.0;
        }

        return (((Point.X - StartX) * SpanX) + ((Point.Y - StartY) * SpanY)) / LengthSquared;
    }

    private enum VertexKind
    {
        Top = 0,
        Right = 1,
        Bottom = 2,
        Left = 3
    }
}
