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
        List<EnterpriseRect> Obstacles = new List<EnterpriseRect>();
        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            if (string.Equals(Node.Id, From.Id, StringComparison.Ordinal)
                || string.Equals(Node.Id, To.Id, StringComparison.Ordinal))
            {
                continue;
            }

            bool AncestorOfHop = IsAncestor(Node, From, Nodes) || IsAncestor(Node, To, Nodes);
            bool Closed = ClosedContainers.Contains(Node.Id);
            if (AncestorOfHop && !Closed)
            {
                EnterpriseRect? Title = TitleBandOf(Node);
                if (Title.HasValue)
                {
                    Obstacles.Add(Title.Value);
                }

                continue;
            }

            if (Node.IsGroup)
            {
                EnterpriseRect? Tab = GroupTabOf(Node);
                if (Tab.HasValue)
                {
                    Obstacles.Add(Tab.Value);
                }

                EnterpriseRect Body = BoundsOf(Node);
                Obstacles.Add(new EnterpriseRect(
                    Body.X,
                    Body.Y + GROUP_TAB_HEIGHT,
                    Body.Width,
                    Math.Max(4, Body.Height - GROUP_TAB_HEIGHT)));
                continue;
            }

            if (Node.Element == null && !Node.IsCompound)
            {
                continue;
            }

            Obstacles.Add(BoundsOf(Node));
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
