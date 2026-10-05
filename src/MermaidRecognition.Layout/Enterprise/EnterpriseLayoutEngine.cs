using System;
using System.Collections.Generic;

using MermaidRecognition.Kinds.Enterprise;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Layout.Layered;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Hierarchical Sugiyama layout for enterprise elements and groups.
/// Serving, realization and assignment are reversed for placement only so
/// consumers sit above providers and Application sits above Technology.
/// </summary>
internal static class EnterpriseLayoutEngine
{
    private const double FONT_CHAR_WIDTH = 6.6;

    private const double LINE_HEIGHT = 15;

    private const double MIN_WIDTH = 120;

    private const double ELEMENT_HEIGHT = 55;

    private const int MAX_CHARS = 22;

    private const double CHILD_PADDING_X = 14;

    private const double CHILD_PADDING_BOTTOM = 14;

    private const double GROUP_TAB_HEIGHT = 18;

    private const double COMPOUND_TOP_PADDING = 36;

    private const double LAYER_SEPARATION = 55;

    private const double NODE_SEPARATION = 35;

    internal static EnterpriseLayoutResult Layout(EnterpriseDocument Document)
    {
        Dictionary<string, EnterpriseLayoutNode> Nodes = new Dictionary<string, EnterpriseLayoutNode>(StringComparer.Ordinal);
        foreach (EnterpriseGroup Group in Document.Groups.Values)
        {
            EnterpriseLayoutNode Node = new EnterpriseLayoutNode(Group.Id, true, null, Group);
            Node.LabelLines.AddRange(Wrap(Group.Label));
            Nodes[Group.Id] = Node;
        }

        foreach (EnterpriseElement Element in Document.Elements.Values)
        {
            EnterpriseLayoutNode Node = new EnterpriseLayoutNode(Element.Id, false, Element, null);
            Node.LabelLines.AddRange(Wrap(Element.Label));
            MeasureLeaf(Node);
            Nodes[Element.Id] = Node;
        }

        List<EnterpriseLayoutNode> Roots = new List<EnterpriseLayoutNode>();
        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            if (!string.IsNullOrEmpty(Node.ParentId) && Nodes.TryGetValue(Node.ParentId, out EnterpriseLayoutNode? Parent))
            {
                Parent.Children.Add(Node);
            }
            else
            {
                Roots.Add(Node);
            }
        }

        LayoutChildren(null, Roots, Document, Nodes);
        AssignAbsolute(Roots, 20, 20);

        double Width = 40;
        double Height = 40;
        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            Width = Math.Max(Width, Node.AbsoluteX + Node.Width + 20);
            Height = Math.Max(Height, Node.AbsoluteY + Node.Height + 20);
        }

        return new EnterpriseLayoutResult(Nodes, Width, Height);
    }

    private static void LayoutChildren(
        string? ParentId,
        List<EnterpriseLayoutNode> Children,
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        foreach (EnterpriseLayoutNode Child in Children)
        {
            if (Child.Children.Count > 0)
            {
                LayoutChildren(Child.Id, Child.Children, Document, Nodes);
                SizeCompound(Child);
            }
            else if (Child.IsGroup)
            {
                Child.Width = Math.Max(MIN_WIDTH, MaxLineWidth(Child.LabelLines) + 24);
                Child.Height = 60;
            }
        }

        if (Children.Count == 0)
        {
            return;
        }

        PlaceSiblings(ParentId, Children, Document, Nodes);
    }

    private static void SizeCompound(EnterpriseLayoutNode Node)
    {
        double MaxRight = 0;
        double MaxBottom = 0;
        foreach (EnterpriseLayoutNode Child in Node.Children)
        {
            MaxRight = Math.Max(MaxRight, Child.RelativeX + Child.Width);
            MaxBottom = Math.Max(MaxBottom, Child.RelativeY + Child.Height);
        }

        double ContentWidth = MaxRight + CHILD_PADDING_X;
        double ContentHeight = MaxBottom + CHILD_PADDING_BOTTOM;
        if (!Node.IsGroup)
        {
            MeasureLeaf(Node);
            Node.Width = Math.Max(Node.Width, Math.Max(MIN_WIDTH, ContentWidth));
            Node.Height = Math.Max(Node.Height, ContentHeight);
        }
        else
        {
            Node.Width = Math.Max(MIN_WIDTH, Math.Max(ContentWidth, MaxLineWidth(Node.LabelLines) + 24));
            Node.Height = Math.Max(60, ContentHeight);
        }
    }

    private static void PlaceSiblings(
        string? ParentId,
        List<EnterpriseLayoutNode> Children,
        EnterpriseDocument Document,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (Children.Count == 1)
        {
            Children[0].RelativeX = CHILD_PADDING_X;
            Children[0].RelativeY = ParentId == null ? 0 : (Nodes[ParentId].IsGroup ? GROUP_TAB_HEIGHT + 14 : COMPOUND_TOP_PADDING);
            return;
        }

        try
        {
            GeometryGraph Graph = new GeometryGraph();
            Dictionary<string, Node> GeometryById = new Dictionary<string, Node>(StringComparer.Ordinal);
            bool Swap = Document.Direction == EnterpriseDiagramDirection.Lr;
            foreach (EnterpriseLayoutNode Child in Children)
            {
                double LayoutWidth = Swap ? Child.Height : Child.Width;
                double LayoutHeight = Swap ? Child.Width : Child.Height;
                ICurve Boundary = CurveFactory.CreateRectangle(LayoutWidth, LayoutHeight, new Point(0, 0));
                Node GeometryNode = new Node(Boundary, Child.Id)
                {
                    UserData = Child.Id
                };
                Graph.Nodes.Add(GeometryNode);
                GeometryById[Child.Id] = GeometryNode;
            }

            HashSet<string> AddedEdges = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnterpriseRelationship Relationship in Document.Relationships)
            {
                if (Relationship.HiddenByNesting)
                {
                    continue;
                }

                string? SourceMember = LiftToChild(Relationship.SourceId, ParentId, Nodes);
                string? TargetMember = LiftToChild(Relationship.TargetId, ParentId, Nodes);
                if (SourceMember == null || TargetMember == null || SourceMember == TargetMember)
                {
                    continue;
                }

                if (!Document.Elements.TryGetValue(Relationship.SourceId, out EnterpriseElement? SourceElement)
                    || !Document.Elements.TryGetValue(Relationship.TargetId, out EnterpriseElement? TargetElement))
                {
                    continue;
                }

                OrientForLayout(Relationship.Kind, SourceElement, TargetElement, SourceMember, TargetMember, out string LayoutSource, out string LayoutTarget);
                string EdgeKey = LayoutSource + "->" + LayoutTarget;
                if (!AddedEdges.Add(EdgeKey))
                {
                    continue;
                }

                if (!GeometryById.TryGetValue(LayoutSource, out Node? SourceNode)
                    || !GeometryById.TryGetValue(LayoutTarget, out Node? TargetNode))
                {
                    continue;
                }

                Graph.Edges.Add(new Edge(SourceNode, TargetNode));
            }

            SugiyamaLayoutSettings Settings = new SugiyamaLayoutSettings
            {
                LayerSeparation = LAYER_SEPARATION,
                NodeSeparation = NODE_SEPARATION
            };
            LayeredLayout Layout = new LayeredLayout(Graph, Settings);
            Layout.Run();

            double MinX = double.MaxValue;
            double MinY = double.MaxValue;
            double MaxY = double.MinValue;
            Dictionary<string, (double Left, double Top)> Raw = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
            foreach (EnterpriseLayoutNode Child in Children)
            {
                Node GeometryNode = GeometryById[Child.Id];
                double CenterX = Swap ? GeometryNode.Center.Y : GeometryNode.Center.X;
                double CenterY = Swap ? GeometryNode.Center.X : GeometryNode.Center.Y;
                double Left = CenterX - (Child.Width / 2.0);
                double Top = CenterY - (Child.Height / 2.0);
                Raw[Child.Id] = (Left, Top);
                MinX = Math.Min(MinX, Left);
                MinY = Math.Min(MinY, Top);
                MaxY = Math.Max(MaxY, Top + Child.Height);
            }

            double OriginX = CHILD_PADDING_X;
            double OriginY = ParentId == null ? 0 : (Nodes[ParentId].IsGroup ? GROUP_TAB_HEIGHT + 14 : COMPOUND_TOP_PADDING);
            foreach (EnterpriseLayoutNode Child in Children)
            {
                (double Left, double Top) = Raw[Child.Id];
                Child.RelativeX = Left - MinX + OriginX;
                Child.RelativeY = (MaxY - (Top + Child.Height)) + OriginY;
            }
        }
        catch (Exception)
        {
            PlaceInGrid(ParentId, Children, Nodes);
        }
    }

    private static void PlaceInGrid(string? ParentId, List<EnterpriseLayoutNode> Children, Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        double OriginY = ParentId == null ? 0 : (Nodes[ParentId].IsGroup ? GROUP_TAB_HEIGHT + 14 : COMPOUND_TOP_PADDING);
        double X = CHILD_PADDING_X;
        double Y = OriginY;
        int Column = 0;
        double RowHeight = 0;
        foreach (EnterpriseLayoutNode Child in Children)
        {
            Child.RelativeX = X;
            Child.RelativeY = Y;
            X += Child.Width + NODE_SEPARATION;
            RowHeight = Math.Max(RowHeight, Child.Height);
            Column++;
            if (Column >= 3)
            {
                Column = 0;
                X = CHILD_PADDING_X;
                Y += RowHeight + LAYER_SEPARATION;
                RowHeight = 0;
            }
        }
    }

    private static void AssignAbsolute(List<EnterpriseLayoutNode> Nodes, double OriginX, double OriginY)
    {
        foreach (EnterpriseLayoutNode Node in Nodes)
        {
            Node.AbsoluteX = OriginX + Node.RelativeX;
            Node.AbsoluteY = OriginY + Node.RelativeY;
            if (Node.Children.Count > 0)
            {
                AssignAbsolute(Node.Children, Node.AbsoluteX, Node.AbsoluteY);
            }
        }
    }

    private static string? LiftToChild(string Id, string? ParentId, Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        string Current = Id;
        HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
        while (Seen.Add(Current) && Nodes.TryGetValue(Current, out EnterpriseLayoutNode? Node))
        {
            if (string.Equals(Node.ParentId, ParentId, StringComparison.Ordinal)
                || (ParentId == null && string.IsNullOrEmpty(Node.ParentId)))
            {
                return Current;
            }

            if (string.IsNullOrEmpty(Node.ParentId))
            {
                return null;
            }

            Current = Node.ParentId;
        }

        return null;
    }

    private static void OrientForLayout(
        EnterpriseRelationshipKind Kind,
        EnterpriseElement Source,
        EnterpriseElement Target,
        string SourceMember,
        string TargetMember,
        out string LayoutSource,
        out string LayoutTarget)
    {
        int SourceRank = EnterpriseElementCatalog.Elements[Source.ElementType].Layer == EnterpriseLayer.Application ? 0 : 1;
        int TargetRank = EnterpriseElementCatalog.Elements[Target.ElementType].Layer == EnterpriseLayer.Application ? 0 : 1;
        if (SourceRank != TargetRank)
        {
            if (SourceRank < TargetRank)
            {
                LayoutSource = SourceMember;
                LayoutTarget = TargetMember;
            }
            else
            {
                LayoutSource = TargetMember;
                LayoutTarget = SourceMember;
            }

            return;
        }

        if (Kind == EnterpriseRelationshipKind.Serving
            || Kind == EnterpriseRelationshipKind.Realization
            || Kind == EnterpriseRelationshipKind.Assignment)
        {
            LayoutSource = TargetMember;
            LayoutTarget = SourceMember;
            return;
        }

        LayoutSource = SourceMember;
        LayoutTarget = TargetMember;
    }

    private static void MeasureLeaf(EnterpriseLayoutNode Node)
    {
        if (Node.Element == null)
        {
            return;
        }

        bool Is3d = EnterpriseElementCatalog.Elements[Node.Element.ElementType].BoxKind == EnterpriseBoxKind.Box3d;
        double Width = Math.Max(MIN_WIDTH, MaxLineWidth(Node.LabelLines) + 40) + (Is3d ? 28 : 0);
        double Height = Math.Max(ELEMENT_HEIGHT, (Node.LabelLines.Count * LINE_HEIGHT) + 26) + (Is3d ? 8 : 0);
        Node.Width = Width;
        Node.Height = Height;
    }

    private static double MaxLineWidth(List<string> Lines)
    {
        double Max = 0;
        foreach (string Line in Lines)
        {
            Max = Math.Max(Max, Line.Length * FONT_CHAR_WIDTH);
        }

        return Max;
    }

    private static List<string> Wrap(string Label)
    {
        List<string> Lines = new List<string>();
        if (string.IsNullOrEmpty(Label))
        {
            Lines.Add(string.Empty);
            return Lines;
        }

        string[] Words = Label.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string Current = string.Empty;
        foreach (string Word in Words)
        {
            string Candidate = Current.Length == 0 ? Word : Current + " " + Word;
            if (Current.Length > 0 && Candidate.Length > MAX_CHARS)
            {
                Lines.Add(Current);
                Current = Word;
            }
            else
            {
                Current = Candidate;
            }
        }

        if (Current.Length > 0)
        {
            Lines.Add(Current);
        }

        if (Lines.Count == 0)
        {
            Lines.Add(Label);
        }

        return Lines;
    }
}

/// <summary>
/// Positions produced by <see cref="EnterpriseLayoutEngine"/>.
/// </summary>
internal sealed class EnterpriseLayoutResult
{
    private readonly Dictionary<string, EnterpriseLayoutNode> _Nodes;

    private readonly double _Width;

    private readonly double _Height;

    internal EnterpriseLayoutResult(Dictionary<string, EnterpriseLayoutNode> Nodes, double Width, double Height)
    {
        _Nodes = Nodes;
        _Width = Width;
        _Height = Height;
    }

    internal Dictionary<string, EnterpriseLayoutNode> Nodes
    {
        get
        {
            return _Nodes;
        }
    }

    internal double Width
    {
        get
        {
            return _Width;
        }
    }

    internal double Height
    {
        get
        {
            return _Height;
        }
    }
}
