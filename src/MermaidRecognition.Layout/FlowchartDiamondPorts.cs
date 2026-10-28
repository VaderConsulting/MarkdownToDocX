using System;
using System.Collections.Generic;
using System.Linq;

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
    /// <param name="DepartureX">Horizontal component of the edge normal.</param>
    /// <param name="DepartureY">Vertical component of the edge normal.</param>
    public FlowchartDiamondPort(
        int OffsetX,
        int OffsetY,
        bool IsVertex,
        FlowchartDiamondEdge Edge,
        double DepartureX,
        double DepartureY)
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

    private readonly double _DepartureX;

    private readonly double _DepartureY;

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
    /// Horizontal component of the outward normal from the slanted edge.
    /// </summary>
    public double DepartureX
    {
        get
        {
            return _DepartureX;
        }
    }

    /// <summary>
    /// Vertical component of the outward normal from the slanted edge.
    /// </summary>
    public double DepartureY
    {
        get
        {
            return _DepartureY;
        }
    }
}

/// <summary>
/// Symmetric ports on the four slanted edges of a decision diamond.
/// </summary>
public static class FlowchartDiamondPorts
{
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
    /// quarter points. Larger counts remain supported without a fixed maximum.
    /// </summary>
    /// <param name="WidthPx">Diamond cell width.</param>
    /// <param name="HeightPx">Diamond cell height.</param>
    /// <param name="Edge">Which slanted edge.</param>
    /// <param name="PortCount">How many evenly spaced ports to return.</param>
    /// <param name="Ports">Ports ordered from the edge start toward the edge end.</param>
    /// <returns>False when the count is below 1 or the edge is a vertex.</returns>
    public static bool TryGetEdgePorts(
        int WidthPx,
        int HeightPx,
        FlowchartDiamondEdge Edge,
        int PortCount,
        out IReadOnlyList<(int X, int Y)> Ports)
    {
        Ports = [];
        if (PortCount < 1 || Edge == FlowchartDiamondEdge.None)
        {
            return false;
        }

        if (!TryGetEdgeEnds(WidthPx, HeightPx, Edge, out double StartX, out double StartY, out double EndX, out double EndY))
        {
            return false;
        }

        List<(int X, int Y)> Built = [];
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
    /// Gives a lone axis-facing connection its diamond tip; otherwise assigns
    /// destinations to facing slanted edges and distributes each edge's ports
    /// at k/(n+1), ordered by the peer's position along the edge.
    /// </summary>
    /// <param name="WidthPx">Diamond cell width.</param>
    /// <param name="HeightPx">Diamond cell height.</param>
    /// <param name="Destinations">Other-node positions relative to the diamond centre.</param>
    /// <param name="Ports">One port per destination, in the same order.</param>
    /// <param name="ReservedVertices">Indices whose legal straight routes reserve an axis-facing tip before slanted-edge allocation.</param>
    /// <returns>True when the destinations can be assigned.</returns>
    public static bool TryAssign(
        int WidthPx,
        int HeightPx,
        IReadOnlyList<(double X, double Y)> Destinations,
        out IReadOnlyList<FlowchartDiamondPort> Ports,
        IReadOnlySet<int>? ReservedVertices = null)
    {
        Ports = [];
        if (Destinations.Count == 0)
        {
            return true;
        }

        List<int>[] EdgeLines = new List<int>[4];
        for (int EdgeIndex = 0; EdgeIndex < EdgeLines.Length; EdgeIndex++)
        {
            EdgeLines[EdgeIndex] = [];
        }

        FlowchartDiamondPort[] Assigned = new FlowchartDiamondPort[Destinations.Count];
        for (int Index = 0; Index < Destinations.Count; Index++)
        {
            (double X, double Y) Destination = Destinations[Index];
            // A lone axis-facing connection owns the tip, just as a lone box
            // connection owns its side centre. Other ends on that nominal side
            // participate in symmetric sloped-edge allocation instead.
            bool Horizontal = Math.Abs(Destination.Y) < 0.001 && Math.Abs(Destination.X) > 0.001;
            bool Vertical = Math.Abs(Destination.X) < 0.001 && Math.Abs(Destination.Y) > 0.001;
            bool Reserved = ReservedVertices?.Contains(Index) == true
                && !Enumerable.Range(0, Index).Any(Other => ReservedVertices.Contains(Other)
                    && (Horizontal ? Math.Abs(Destinations[Other].Y) < 0.001 && Math.Sign(Destinations[Other].X) == Math.Sign(Destination.X)
                        : Math.Abs(Destinations[Other].X) < 0.001 && Math.Sign(Destinations[Other].Y) == Math.Sign(Destination.Y)));
            if ((Horizontal || Vertical) && (Reserved || Destinations.Count(Other =>
                    Horizontal ? Math.Abs(Other.X) >= Math.Abs(Other.Y) && Math.Sign(Other.X) == Math.Sign(Destination.X)
                        : Math.Abs(Other.Y) > Math.Abs(Other.X) && Math.Sign(Other.Y) == Math.Sign(Destination.Y)) == 1))
            {
                int Nx = Horizontal ? Math.Sign(Destination.X) : 0;
                int Ny = Vertical ? Math.Sign(Destination.Y) : 0;
                Assigned[Index] = new(Nx * WidthPx / 2, Ny * HeightPx / 2, true, FlowchartDiamondEdge.None, Nx, Ny);
                continue;
            }
            EdgeLines[(int)NearestEdge(Destinations[Index], WidthPx, HeightPx) - 1].Add(Index);
        }

        for (int EdgeIndex = 0; EdgeIndex < EdgeLines.Length; EdgeIndex++)
        {
            List<int> Lines = EdgeLines[EdgeIndex];
            if (Lines.Count == 0)
            {
                continue;
            }

            FlowchartDiamondEdge Edge = (FlowchartDiamondEdge)(EdgeIndex + 1);
            if (!TryGetEdgePorts(WidthPx, HeightPx, Edge, Lines.Count, out IReadOnlyList<(int X, int Y)> EdgePorts))
            {
                return false;
            }

            Lines.Sort(delegate(int Left, int Right)
            {
                double LeftT = ParameterAlongDiamondEdge(Destinations[Left], WidthPx, HeightPx, Edge);
                double RightT = ParameterAlongDiamondEdge(Destinations[Right], WidthPx, HeightPx, Edge);
                int Position = LeftT.CompareTo(RightT);
                return Position != 0 ? Position : Left.CompareTo(Right);
            });

            for (int LinePosition = 0; LinePosition < Lines.Count; LinePosition++)
            {
                int LineIndex = Lines[LinePosition];
                (int X, int Y) = EdgePorts[LinePosition];
                (double DepartureX, double DepartureY) = EdgeNormal(Edge, WidthPx, HeightPx);
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

    private static FlowchartDiamondEdge NearestEdge((double X, double Y) Destination, int WidthPx, int HeightPx)
    {
        double HalfWidth = WidthPx / 2.0, HalfHeight = HeightPx / 2.0;
        (FlowchartDiamondEdge Edge, double X, double Y)[] Centres =
        [
            (FlowchartDiamondEdge.TopRight, HalfWidth / 2, HalfHeight / 2),
            (FlowchartDiamondEdge.BottomRight, HalfWidth / 2, -HalfHeight / 2),
            (FlowchartDiamondEdge.BottomLeft, -HalfWidth / 2, -HalfHeight / 2),
            (FlowchartDiamondEdge.TopLeft, -HalfWidth / 2, HalfHeight / 2)
        ];
        return Centres.OrderBy(Centre => Math.Pow(Destination.X - Centre.X, 2) + Math.Pow(Destination.Y - Centre.Y, 2))
            .ThenBy(Centre => (int)Centre.Edge).First().Edge;
    }

    private static (double X, double Y) EdgeNormal(FlowchartDiamondEdge Edge, int WidthPx, int HeightPx)
    {
        double X = HeightPx, Y = WidthPx;
        return Edge switch
        {
            FlowchartDiamondEdge.TopRight => (X, Y),
            FlowchartDiamondEdge.BottomRight => (X, -Y),
            FlowchartDiamondEdge.BottomLeft => (-X, -Y),
            FlowchartDiamondEdge.TopLeft => (-X, Y),
            _ => (0, 1)
        };
    }

    private static int[] SelectOrderedPorts(IReadOnlyList<double> Parameters, int PortCount)
    {
        int LineCount = Parameters.Count;
        double[,] Cost = new double[LineCount + 1, PortCount + 1];
        bool[,] Took = new bool[LineCount + 1, PortCount + 1];
        for (int Line = 1; Line <= LineCount; Line++)
        {
            for (int Port = 0; Port <= PortCount; Port++)
            {
                Cost[Line, Port] = double.PositiveInfinity;
            }
        }

        for (int Port = 1; Port <= PortCount; Port++)
        {
            Cost[0, Port] = 0.0;
            for (int Line = 1; Line <= Math.Min(LineCount, Port); Line++)
            {
                double Skip = Cost[Line, Port - 1];
                double Fraction = Port / (double)(PortCount + 1);
                double Take = Cost[Line - 1, Port - 1] + Math.Abs(Parameters[Line - 1] - Fraction);
                // Prefer the earlier slot on an exact tie. It makes the result
                // stable while preserving the declared order of coincident rays.
                if (Take < Skip - 0.0000001)
                {
                    Cost[Line, Port] = Take;
                    Took[Line, Port] = true;
                }
                else
                {
                    Cost[Line, Port] = Skip;
                }
            }
        }

        int[] Selected = new int[LineCount];
        int RemainingLines = LineCount;
        int RemainingPorts = PortCount;
        while (RemainingLines > 0)
        {
            if (Took[RemainingLines, RemainingPorts])
            {
                Selected[RemainingLines - 1] = RemainingPorts - 1;
                RemainingLines--;
            }
            RemainingPorts--;
        }

        return Selected;
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

    private static bool EdgeTouchesVertex(FlowchartDiamondEdge Edge, VertexKind Vertex)
    {
        return Vertex switch
        {
            VertexKind.Top => Edge == FlowchartDiamondEdge.TopLeft || Edge == FlowchartDiamondEdge.TopRight,
            VertexKind.Right => Edge == FlowchartDiamondEdge.TopRight || Edge == FlowchartDiamondEdge.BottomRight,
            VertexKind.Bottom => Edge == FlowchartDiamondEdge.BottomRight || Edge == FlowchartDiamondEdge.BottomLeft,
            _ => Edge == FlowchartDiamondEdge.BottomLeft || Edge == FlowchartDiamondEdge.TopLeft
        };
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

    private static double ParameterAlongDiamondEdge(
        (double X, double Y) Point,
        int WidthPx,
        int HeightPx,
        FlowchartDiamondEdge Edge)
    {
        double NormalX = Point.X / Math.Max(1.0, WidthPx / 2.0);
        double NormalY = Point.Y / Math.Max(1.0, HeightPx / 2.0);
        double Horizontal = Math.Abs(NormalX);
        double Vertical = Math.Abs(NormalY);
        double Total = Horizontal + Vertical;
        if (Total <= 0.0)
        {
            return 0.5;
        }

        double Parameter = Edge == FlowchartDiamondEdge.TopRight
            || Edge == FlowchartDiamondEdge.BottomLeft
            ? Horizontal / Total
            : Vertical / Total;
        return Math.Max(0.0, Math.Min(1.0, Parameter));
    }

    private enum VertexKind
    {
        Top = 0,
        Right = 1,
        Bottom = 2,
        Left = 3
    }
}
