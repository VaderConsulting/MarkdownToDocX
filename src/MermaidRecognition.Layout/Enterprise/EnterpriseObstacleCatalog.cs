using System;
using System.Collections.Generic;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Node, title-band and group-tab rectangles used as enterprise routing obstacles.
/// </summary>
internal static class EnterpriseObstacleCatalog
{
    internal const double GROUP_TAB_HEIGHT = 18;

    internal const double COMPOUND_TITLE_HEIGHT = 22;

    internal const double GROUP_TAB_CHAR_WIDTH = 6.6;

    internal const double JUNCTION_SIZE = 26;

    internal static EnterpriseRect BoundsOf(EnterpriseLayoutNode Node)
    {
        if (Node.Element != null && Node.Element.ElementType == EnterpriseElementType.Junction)
        {
            return new EnterpriseRect(Node.AbsoluteX, Node.AbsoluteY, JUNCTION_SIZE, JUNCTION_SIZE);
        }

        return new EnterpriseRect(Node.AbsoluteX, Node.AbsoluteY, Node.Width, Node.Height);
    }

    internal static EnterpriseRect? TitleBandOf(EnterpriseLayoutNode Node)
    {
        if (Node.IsGroup)
        {
            return GroupTabOf(Node);
        }

        if (!Node.IsCompound)
        {
            return null;
        }

        return new EnterpriseRect(Node.AbsoluteX, Node.AbsoluteY, Node.Width, COMPOUND_TITLE_HEIGHT);
    }

    /// <summary>
    /// Text band of a leaf label, compound title, or group tab.
    /// </summary>
    /// <param name="Node">Laid-out node.</param>
    /// <returns>Approximate label rectangle, or null when the node has no visible text.</returns>
    internal static EnterpriseRect? LabelBandOf(EnterpriseLayoutNode Node)
    {
        if (Node.IsGroup || Node.IsCompound)
        {
            return TitleBandOf(Node);
        }

        if (Node.Element == null)
        {
            return null;
        }

        if (Node.Element.ElementType == EnterpriseElementType.Junction)
        {
            string JunctionLabel = Node.DisplayLabel;
            if (string.IsNullOrEmpty(JunctionLabel) || string.Equals(JunctionLabel, Node.Id, StringComparison.Ordinal))
            {
                return null;
            }

            double JunctionWidth = Math.Max(24, JunctionLabel.Length * 6.6);
            return new EnterpriseRect(Node.AbsoluteX + 28, Node.AbsoluteY + 4, JunctionWidth, 18);
        }

        EnterpriseElementMetadata Metadata = EnterpriseElementCatalog.Elements[Node.Element.ElementType];
        bool Is3d = Metadata.BoxKind == EnterpriseBoxKind.Box3d;
        bool IsObject = Metadata.BoxKind == EnterpriseBoxKind.Object;
        double TextWidth = 24;
        foreach (string Line in Node.LabelLines)
        {
            TextWidth = Math.Max(TextWidth, (Line.Length * 6.6) + 8);
        }

        double TextHeight = Math.Max(14, Node.LabelLines.Count * 15.0);
        double CentreX = Node.AbsoluteX + (Is3d ? (Node.Width - 30) / 2.0 : Node.Width / 2.0);
        double Top;
        if (IsObject)
        {
            Top = Node.AbsoluteY + 12 + ((Node.Height - 12) / 2.0) - (((Node.LabelLines.Count - 1) * 15.0) / 2.0) + 4;
        }
        else
        {
            Top = Node.AbsoluteY + (Node.Height / 2.0) + (Is3d ? 4 : 0) - (((Node.LabelLines.Count - 1) * 15.0) / 2.0) + 4;
        }

        return new EnterpriseRect(CentreX - (TextWidth / 2.0), Top - 12, TextWidth, TextHeight);
    }

    internal static EnterpriseRect? GroupTabOf(EnterpriseLayoutNode Node)
    {
        if (!Node.IsGroup)
        {
            return null;
        }

        double TabWidth = Math.Max(60, (Node.DisplayLabel.Length * GROUP_TAB_CHAR_WIDTH) + 16);
        TabWidth = Math.Min(TabWidth, Math.Max(20, Node.Width - 8));
        return new EnterpriseRect(Node.AbsoluteX, Node.AbsoluteY, TabWidth, GROUP_TAB_HEIGHT);
    }

    internal static bool IsAncestor(
        EnterpriseLayoutNode Candidate,
        EnterpriseLayoutNode Node,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        string? ParentId = Node.ParentId;
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(ParentId) && Seen.Add(ParentId) && Nodes.TryGetValue(ParentId, out EnterpriseLayoutNode? Parent))
        {
            if (string.Equals(Parent.Id, Candidate.Id, StringComparison.Ordinal))
            {
                return true;
            }

            ParentId = Parent.ParentId;
        }

        return false;
    }

    internal static List<EnterpriseLayoutNode> CrossingContainers(
        EnterpriseLayoutNode Inner,
        EnterpriseLayoutNode Other,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<EnterpriseLayoutNode> Containers = new List<EnterpriseLayoutNode>();
        string? ParentId = Inner.ParentId;
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(ParentId) && Seen.Add(ParentId) && Nodes.TryGetValue(ParentId, out EnterpriseLayoutNode? Parent))
        {
            if (!IsAncestor(Parent, Other, Nodes) && !string.Equals(Parent.Id, Other.Id, StringComparison.Ordinal))
            {
                Containers.Add(Parent);
            }

            ParentId = Parent.ParentId;
        }

        return Containers;
    }

    internal static List<EnterpriseRect> ObstaclesForHop(
        EnterpriseLayoutNode From,
        EnterpriseLayoutNode To,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        HashSet<string> ClosedContainers)
    {
        return ObstaclesForHop(From, To, Nodes, ClosedContainers, false);
    }

    /// <summary>
    /// Obstacles for one hop, optionally treating the endpoints' bodies and label bands as blocked.
    /// Non-endpoint boxes are inflated by the border gap so corridors stay off the stroke.
    /// </summary>
    /// <param name="From">Hop source.</param>
    /// <param name="To">Hop target.</param>
    /// <param name="Nodes">Laid-out nodes.</param>
    /// <param name="ClosedContainers">Containers already exited on earlier hops.</param>
    /// <param name="IncludeEndpointBodies">When true, the endpoints' interiors and labels are obstacles.</param>
    /// <returns>Obstacle rectangles.</returns>
    internal static List<EnterpriseRect> ObstaclesForHop(
        EnterpriseLayoutNode From,
        EnterpriseLayoutNode To,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        HashSet<string> ClosedContainers,
        bool IncludeEndpointBodies)
    {
        List<EnterpriseRect> Obstacles = new List<EnterpriseRect>();
        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            bool IsEndpoint = string.Equals(Node.Id, From.Id, StringComparison.Ordinal)
                || string.Equals(Node.Id, To.Id, StringComparison.Ordinal);
            if (IsEndpoint)
            {
                if (IncludeEndpointBodies)
                {
                    Obstacles.Add(BoundsOf(Node).Inflate(-1));
                    EnterpriseRect? OwnLabel = LabelBandOf(Node);
                    if (OwnLabel.HasValue)
                    {
                        Obstacles.Add(OwnLabel.Value);
                    }

                    EnterpriseRect? OwnTitle = TitleBandOf(Node);
                    if (OwnTitle.HasValue)
                    {
                        Obstacles.Add(OwnTitle.Value);
                    }
                }

                continue;
            }

            bool AncestorOfHop = IsAncestor(Node, From, Nodes) || IsAncestor(Node, To, Nodes);
            bool Closed = ClosedContainers.Contains(Node.Id);
            if (AncestorOfHop && !Closed)
            {
                EnterpriseRect? Title = TitleBandOf(Node);
                if (Title.HasValue)
                {
                    Obstacles.Add(Title.Value.Inflate(2));
                }

                continue;
            }

            if (Node.IsGroup)
            {
                EnterpriseRect? Tab = GroupTabOf(Node);
                if (Tab.HasValue)
                {
                    Obstacles.Add(Tab.Value.Inflate(EnterpriseOrthogonalRouter.BORDER_GAP));
                }

                EnterpriseRect Body = BoundsOf(Node);
                Obstacles.Add(new EnterpriseRect(
                    Body.X,
                    Body.Y + GROUP_TAB_HEIGHT,
                    Body.Width,
                    Math.Max(4, Body.Height - GROUP_TAB_HEIGHT)).Inflate(EnterpriseOrthogonalRouter.BORDER_GAP));
                continue;
            }

            if (Node.Element == null && !Node.IsCompound)
            {
                continue;
            }

            Obstacles.Add(BoundsOf(Node).Inflate(EnterpriseOrthogonalRouter.BORDER_GAP));
        }

        return Obstacles;
    }
}

/// <summary>
/// Axis-aligned rectangle in SVG coordinates.
/// </summary>
internal readonly struct EnterpriseRect
{
    internal EnterpriseRect(double X, double Y, double Width, double Height)
    {
        this.X = X;
        this.Y = Y;
        this.Width = Width;
        this.Height = Height;
    }

    internal double X { get; }

    internal double Y { get; }

    internal double Width { get; }

    internal double Height { get; }

    internal double Right
    {
        get
        {
            return X + Width;
        }
    }

    internal double Bottom
    {
        get
        {
            return Y + Height;
        }
    }

    internal double CentreX
    {
        get
        {
            return X + (Width / 2.0);
        }
    }

    internal double CentreY
    {
        get
        {
            return Y + (Height / 2.0);
        }
    }

    internal bool Contains(double PointX, double PointY, double Inset)
    {
        return PointX >= X + Inset
            && PointX <= Right - Inset
            && PointY >= Y + Inset
            && PointY <= Bottom - Inset;
    }

    internal EnterpriseRect Inflate(double Amount)
    {
        return new EnterpriseRect(X - Amount, Y - Amount, Width + (Amount * 2.0), Height + (Amount * 2.0));
    }

    internal bool IsAlongBorder(double X1, double Y1, double X2, double Y2, double Gap, double MinRun)
    {
        bool Horizontal = Math.Abs(Y1 - Y2) < 0.6;
        bool Vertical = Math.Abs(X1 - X2) < 0.6;
        if (Horizontal)
        {
            double MidY = (Y1 + Y2) / 2.0;
            double Overlap = Math.Min(Math.Max(X1, X2), Right) - Math.Max(Math.Min(X1, X2), X);
            if (Overlap < MinRun)
            {
                return false;
            }

            return Math.Abs(MidY - Y) < Gap || Math.Abs(MidY - Bottom) < Gap;
        }

        if (Vertical)
        {
            double MidX = (X1 + X2) / 2.0;
            double Overlap = Math.Min(Math.Max(Y1, Y2), Bottom) - Math.Max(Math.Min(Y1, Y2), Y);
            if (Overlap < MinRun)
            {
                return false;
            }

            return Math.Abs(MidX - X) < Gap || Math.Abs(MidX - Right) < Gap;
        }

        return false;
    }

    internal bool ContainsOnBorder(double PointX, double PointY, double Tolerance)
    {
        bool InsideX = PointX >= X - Tolerance && PointX <= Right + Tolerance;
        bool InsideY = PointY >= Y - Tolerance && PointY <= Bottom + Tolerance;
        if (!InsideX || !InsideY)
        {
            return false;
        }

        bool OnVertical = Math.Abs(PointX - X) <= Tolerance || Math.Abs(PointX - Right) <= Tolerance;
        bool OnHorizontal = Math.Abs(PointY - Y) <= Tolerance || Math.Abs(PointY - Bottom) <= Tolerance;
        return OnVertical || OnHorizontal;
    }

    internal bool IntersectsSegment(double X1, double Y1, double X2, double Y2, double Inset)
    {
        double Left = X + Inset;
        double Top = Y + Inset;
        double RightEdge = Right - Inset;
        double BottomEdge = Bottom - Inset;
        if (RightEdge <= Left || BottomEdge <= Top)
        {
            return false;
        }

        if (Contains(X1, Y1, Inset) || Contains(X2, Y2, Inset))
        {
            return true;
        }

        return SegmentHits(X1, Y1, X2, Y2, Left, Top, RightEdge, Top)
            || SegmentHits(X1, Y1, X2, Y2, RightEdge, Top, RightEdge, BottomEdge)
            || SegmentHits(X1, Y1, X2, Y2, RightEdge, BottomEdge, Left, BottomEdge)
            || SegmentHits(X1, Y1, X2, Y2, Left, BottomEdge, Left, Top);
    }

    private static bool SegmentHits(
        double Ax,
        double Ay,
        double Bx,
        double By,
        double Cx,
        double Cy,
        double Dx,
        double Dy)
    {
        double Denominator = ((Bx - Ax) * (Dy - Cy)) - ((By - Ay) * (Dx - Cx));
        if (Math.Abs(Denominator) < 0.0001)
        {
            return false;
        }

        double U = (((Cx - Ax) * (Dy - Cy)) - ((Cy - Ay) * (Dx - Cx))) / Denominator;
        double V = (((Cx - Ax) * (By - Ay)) - ((Cy - Ay) * (Bx - Ax))) / Denominator;
        return U >= 0 && U <= 1 && V >= 0 && V <= 1;
    }
}

internal enum EnterprisePortSide
{
    Top,
    Right,
    Bottom,
    Left
}

internal readonly struct EnterprisePort
{
    internal EnterprisePort(double X, double Y, EnterprisePortSide Side)
    {
        this.X = X;
        this.Y = Y;
        this.Side = Side;
    }

    internal double X { get; }

    internal double Y { get; }

    internal EnterprisePortSide Side { get; }
}
