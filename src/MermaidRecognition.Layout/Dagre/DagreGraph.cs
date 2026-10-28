// Port of graphlib and dagre from dagre-d3-es 7.0.14, the layout library Mermaid calls.
// Original dagre copyright: Copyright (c) 2012-2014 Chris Pettitt
// Original graphlib copyright: Copyright (c) 2012-2014 Chris Pettitt
// Copyright (c) 2022-2024 Thibaut Lassalle, David Newell, Alois Klink, Sidharth Vinod and dagre-es contributors
// Permission is granted under the MIT licence that ships with dagre-d3-es.

using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Dagre;

/// <summary>
/// Point in the dagre coordinate system.
/// </summary>
internal sealed class DagrePoint
{
    public double X { get; set; }

    public double Y { get; set; }
}

/// <summary>
/// Mutable label for one dagre node. Layout shares these references across graph copies.
/// </summary>
internal sealed class DagreNodeLabel
{
    public double Width { get; set; }

    public double Height { get; set; }

    public int? Rank { get; set; }

    public int? Order { get; set; }

    public double X { get; set; }

    public double Y { get; set; }

    public string? Dummy { get; set; }

    public string? BorderTop { get; set; }

    public string? BorderBottom { get; set; }

    public Dictionary<int, string>? BorderLeftByRank { get; set; }

    public Dictionary<int, string>? BorderRightByRank { get; set; }

    public string? LayerBorderLeft { get; set; }

    public string? LayerBorderRight { get; set; }

    public int? MinRank { get; set; }

    public int? MaxRank { get; set; }

    public string? BorderType { get; set; }

    public string? LabelPos { get; set; }

    public DagreEdgeLabel? EdgeLabel { get; set; }

    public DagreEdge? EdgeObj { get; set; }

    public List<DagreSelfEdge>? SelfEdges { get; set; }

    public int? Low { get; set; }

    public int? Lim { get; set; }

    public string? TreeParent { get; set; }

    public bool HasTreeParent { get; set; }

    public double FasIn { get; set; }

    public double FasOut { get; set; }

    public string? FasNodeId { get; set; }

    public DagreNodeLabel? ListNext { get; set; }

    public DagreNodeLabel? ListPrev { get; set; }
}

/// <summary>
/// Mutable label for one dagre edge. A block-graph edge stores a separation number instead.
/// </summary>
internal sealed class DagreEdgeLabel
{
    public bool IsNumber { get; set; }

    public double Number { get; set; }

    public double Weight { get; set; } = 1;

    public int Minlen { get; set; } = 1;

    public double Width { get; set; }

    public double Height { get; set; }

    public double LabelOffset { get; set; } = 10;

    public string LabelPos { get; set; } = "r";

    public bool Reversed { get; set; }

    public string? ForwardName { get; set; }

    public bool NestingEdge { get; set; }

    public int? LabelRank { get; set; }

    public List<DagrePoint> Points { get; } = [];

    public bool HasPoints { get; set; }

    public bool HasX { get; set; }

    public double X { get; set; }

    public bool HasY { get; set; }

    public double Y { get; set; }

    public double? CutValue { get; set; }

    /// <summary>
    /// Original cluster id when this edge leaves a subgraph. Layout uses a stand-in node inside the cluster.
    /// </summary>
    public string? FromCluster { get; set; }

    /// <summary>
    /// Original cluster id when this edge enters a subgraph. Layout uses a stand-in node inside the cluster.
    /// </summary>
    public string? ToCluster { get; set; }

    /// <summary>
    /// Copies the fields dagre keeps when it rebuilds an edge label during layout.
    /// </summary>
    public DagreEdgeLabel Clone()
    {
        DagreEdgeLabel Copy = new DagreEdgeLabel
        {
            IsNumber = IsNumber,
            Number = Number,
            Weight = Weight,
            Minlen = Minlen,
            Width = Width,
            Height = Height,
            LabelOffset = LabelOffset,
            LabelPos = LabelPos,
            Reversed = Reversed,
            ForwardName = ForwardName,
            NestingEdge = NestingEdge,
            LabelRank = LabelRank,
            HasPoints = HasPoints,
            HasX = HasX,
            X = X,
            HasY = HasY,
            Y = Y,
            CutValue = CutValue,
            FromCluster = FromCluster,
            ToCluster = ToCluster
        };
        foreach (DagrePoint Point in Points)
        {
            Copy.Points.Add(new DagrePoint
            {
                X = Point.X,
                Y = Point.Y
            });
        }

        return Copy;
    }
}

/// <summary>
/// Identity of one directed or undirected edge.
/// </summary>
internal sealed class DagreEdge
{
    public string V { get; set; } = string.Empty;

    public string W { get; set; } = string.Empty;

    public string? Name { get; set; }
}

/// <summary>
/// A self-edge removed before ranking and restored afterwards.
/// </summary>
internal sealed class DagreSelfEdge
{
    public DagreEdge Edge { get; set; } = new DagreEdge();

    public DagreEdgeLabel Label { get; set; } = new DagreEdgeLabel();
}

/// <summary>
/// Graph-level dagre attributes.
/// </summary>
internal sealed class DagreGraphLabel
{
    public double NodeSep { get; set; } = 50;

    public double EdgeSep { get; set; } = 20;

    public double RankSep { get; set; } = 50;

    public double MarginX { get; set; }

    public double MarginY { get; set; }

    public string RankDir { get; set; } = "tb";

    public string? Acyclicer { get; set; }

    public string? Ranker { get; set; }

    public string? Align { get; set; }

    public string? NestingRoot { get; set; }

    public int NodeRankFactor { get; set; } = 1;

    public bool HasNodeRankFactor { get; set; }

    public int MaxRank { get; set; }

    public List<string> DummyChains { get; } = [];

    public double Width { get; set; }

    public double Height { get; set; }

    public string? Root { get; set; }
}

/// <summary>
/// Insertion-ordered directed multigraph with the graphlib operations dagre uses.
/// </summary>
internal sealed class DagreGraph
{
    private const string GRAPH_NODE = "\0";

    private const string DEFAULT_EDGE_NAME = "\0";

    private readonly bool _IsDirected;

    private readonly bool _IsMultigraph;

    private readonly bool _IsCompound;

    private readonly Dictionary<string, DagreNodeLabel?> _Nodes = new Dictionary<string, DagreNodeLabel?>(StringComparer.Ordinal);

    private readonly List<string> _NodeOrder = [];

    private readonly Dictionary<string, DagreEdge> _EdgeObjs = new Dictionary<string, DagreEdge>(StringComparer.Ordinal);

    private readonly Dictionary<string, DagreEdgeLabel?> _EdgeLabels = new Dictionary<string, DagreEdgeLabel?>(StringComparer.Ordinal);

    private readonly List<string> _EdgeOrder = [];

    private readonly Dictionary<string, NodeSlots> _Slots = new Dictionary<string, NodeSlots>(StringComparer.Ordinal);

    private readonly Dictionary<string, string> _Parent = new Dictionary<string, string>(StringComparer.Ordinal);

    private readonly Dictionary<string, List<string>> _Children = new Dictionary<string, List<string>>(StringComparer.Ordinal);

    private Func<string, DagreNodeLabel?> _DefaultNodeLabel = static _ => null;

    private DagreGraphLabel _Label = new DagreGraphLabel();

    public DagreGraph(bool Directed = true, bool Multigraph = false, bool Compound = false)
    {
        _IsDirected = Directed;
        _IsMultigraph = Multigraph;
        _IsCompound = Compound;
        if (_IsCompound)
        {
            _Children[GRAPH_NODE] = [];
        }
    }

    public bool IsDirected
    {
        get
        {
            return _IsDirected;
        }
    }

    public bool IsMultigraph
    {
        get
        {
            return _IsMultigraph;
        }
    }

    public DagreGraphLabel Label
    {
        get
        {
            return _Label;
        }
    }

    public void SetGraph(DagreGraphLabel Label)
    {
        _Label = Label;
    }

    public void SetDefaultNodeLabel(Func<string, DagreNodeLabel?> Factory)
    {
        _DefaultNodeLabel = Factory;
    }

    public int NodeCount
    {
        get
        {
            return _NodeOrder.Count;
        }
    }

    public List<string> Nodes()
    {
        return new List<string>(_NodeOrder);
    }

    public bool HasNode(string V)
    {
        return _Nodes.ContainsKey(V);
    }

    public DagreNodeLabel? Node(string? V)
    {
        if (V == null)
        {
            return null;
        }

        if (_Nodes.TryGetValue(V, out DagreNodeLabel? Label))
        {
            return Label;
        }

        return null;
    }

    public void SetNode(string V, DagreNodeLabel? Value, bool ValueSpecified)
    {
        if (_Nodes.ContainsKey(V))
        {
            if (ValueSpecified)
            {
                _Nodes[V] = Value;
            }

            return;
        }

        _Nodes[V] = ValueSpecified ? Value : _DefaultNodeLabel(V);
        _NodeOrder.Add(V);
        _Slots[V] = new NodeSlots();
        if (_IsCompound)
        {
            _Parent[V] = GRAPH_NODE;
            _Children[V] = [];
            _Children[GRAPH_NODE].Add(V);
        }
    }

    public void RemoveNode(string V)
    {
        if (!_Nodes.ContainsKey(V))
        {
            return;
        }

        _Nodes.Remove(V);
        _NodeOrder.Remove(V);
        if (_IsCompound)
        {
            RemoveFromParentList(V);
            _Parent.Remove(V);
            List<string> ChildIds = Children(V);
            foreach (string Child in ChildIds)
            {
                SetParent(Child, null);
            }

            _Children.Remove(V);
        }

        NodeSlots Slots = _Slots[V];
        List<DagreEdge> Incident = [.. Slots.InEdges, .. Slots.OutEdges];
        foreach (DagreEdge Edge in Incident)
        {
            RemoveEdge(Edge);
        }

        _Slots.Remove(V);
    }

    public void SetParent(string V, string? Parent)
    {
        if (!_IsCompound)
        {
            throw new InvalidOperationException("Cannot set parent in a non-compound graph.");
        }

        string ParentId = Parent ?? GRAPH_NODE;
        if (Parent != null)
        {
            SetNode(Parent, null, false);
        }

        SetNode(V, null, false);
        RemoveFromParentList(V);
        _Parent[V] = ParentId;
        _Children[ParentId].Add(V);
    }

    public string? Parent(string V)
    {
        if (!_IsCompound)
        {
            return null;
        }

        if (!_Parent.TryGetValue(V, out string? ParentId))
        {
            return null;
        }

        if (ParentId == GRAPH_NODE)
        {
            return null;
        }

        return ParentId;
    }

    public List<string> Children(string? V = null)
    {
        if (!_IsCompound)
        {
            if (V == null)
            {
                return Nodes();
            }

            if (HasNode(V))
            {
                return [];
            }

            return [];
        }

        string Key = V ?? GRAPH_NODE;
        if (!_Children.TryGetValue(Key, out List<string>? Kids))
        {
            return [];
        }

        return new List<string>(Kids);
    }

    public List<string> Predecessors(string V)
    {
        if (!_Slots.TryGetValue(V, out NodeSlots? Slots))
        {
            return [];
        }

        return new List<string>(Slots.PredOrder);
    }

    public List<string> Successors(string V)
    {
        if (!_Slots.TryGetValue(V, out NodeSlots? Slots))
        {
            return [];
        }

        return new List<string>(Slots.SuccOrder);
    }

    public List<string> Neighbors(string V)
    {
        List<string> Result = Predecessors(V);
        foreach (string Successor in Successors(V))
        {
            if (!Result.Contains(Successor))
            {
                Result.Add(Successor);
            }
        }

        return Result;
    }

    public List<string> Sources()
    {
        List<string> Result = [];
        foreach (string V in _NodeOrder)
        {
            if (_Slots[V].InEdges.Count == 0)
            {
                Result.Add(V);
            }
        }

        return Result;
    }

    public List<DagreEdge> Edges()
    {
        List<DagreEdge> Result = [];
        foreach (string EdgeId in _EdgeOrder)
        {
            Result.Add(_EdgeObjs[EdgeId]);
        }

        return Result;
    }

    public List<DagreEdge> InEdges(string V)
    {
        if (!_Slots.TryGetValue(V, out NodeSlots? Slots))
        {
            return [];
        }

        return new List<DagreEdge>(Slots.InEdges);
    }

    public List<DagreEdge> OutEdges(string V)
    {
        if (!_Slots.TryGetValue(V, out NodeSlots? Slots))
        {
            return [];
        }

        return new List<DagreEdge>(Slots.OutEdges);
    }

    public List<DagreEdge> OutEdgesTo(string V, string W)
    {
        List<DagreEdge> Result = [];
        foreach (DagreEdge Edge in OutEdges(V))
        {
            if (Edge.W == W)
            {
                Result.Add(Edge);
            }
        }

        return Result;
    }

    public List<DagreEdge> NodeEdges(string V)
    {
        List<DagreEdge> Result = InEdges(V);
        Result.AddRange(OutEdges(V));
        return Result;
    }

    public void SetEdge(string V, string W, DagreEdgeLabel? Value, bool ValueSpecified, string? Name = null)
    {
        string EdgeId = EdgeIdOf(V, W, Name, out string StoredV, out string StoredW, out string? StoredName);
        if (_EdgeLabels.ContainsKey(EdgeId))
        {
            if (ValueSpecified)
            {
                _EdgeLabels[EdgeId] = Value;
            }

            return;
        }

        SetNode(StoredV, null, false);
        SetNode(StoredW, null, false);
        _EdgeLabels[EdgeId] = ValueSpecified ? Value : null;
        DagreEdge Edge = new DagreEdge
        {
            V = StoredV,
            W = StoredW,
            Name = StoredName
        };
        _EdgeObjs[EdgeId] = Edge;
        _EdgeOrder.Add(EdgeId);
        AddNeighbour(_Slots[StoredW], _Slots[StoredW].PredOrder, _Slots[StoredW].PredCount, StoredV);
        AddNeighbour(_Slots[StoredV], _Slots[StoredV].SuccOrder, _Slots[StoredV].SuccCount, StoredW);
        _Slots[StoredW].InEdges.Add(Edge);
        _Slots[StoredV].OutEdges.Add(Edge);
    }

    public void SetEdge(DagreEdge Edge, DagreEdgeLabel? Value, bool ValueSpecified)
    {
        SetEdge(Edge.V, Edge.W, Value, ValueSpecified, Edge.Name);
    }

    public DagreEdgeLabel? Edge(string V, string W, string? Name = null)
    {
        string EdgeId = EdgeIdOf(V, W, Name, out _, out _, out _);
        if (_EdgeLabels.TryGetValue(EdgeId, out DagreEdgeLabel? Label))
        {
            return Label;
        }

        return null;
    }

    public DagreEdgeLabel? Edge(DagreEdge Edge)
    {
        return this.Edge(Edge.V, Edge.W, Edge.Name);
    }

    public bool HasEdge(string V, string W, string? Name = null)
    {
        string EdgeId = EdgeIdOf(V, W, Name, out _, out _, out _);
        return _EdgeLabels.ContainsKey(EdgeId);
    }

    public bool HasEdge(DagreEdge Edge)
    {
        return HasEdge(Edge.V, Edge.W, Edge.Name);
    }

    public void RemoveEdge(DagreEdge Edge)
    {
        string EdgeId = EdgeIdOf(Edge.V, Edge.W, Edge.Name, out _, out _, out _);
        if (!_EdgeObjs.TryGetValue(EdgeId, out DagreEdge? Stored))
        {
            return;
        }

        _EdgeLabels.Remove(EdgeId);
        _EdgeObjs.Remove(EdgeId);
        _EdgeOrder.Remove(EdgeId);
        if (_Slots.TryGetValue(Stored.W, out NodeSlots? TargetSlots))
        {
            RemoveNeighbour(TargetSlots.PredOrder, TargetSlots.PredCount, Stored.V);
            TargetSlots.InEdges.Remove(Stored);
        }

        if (_Slots.TryGetValue(Stored.V, out NodeSlots? SourceSlots))
        {
            RemoveNeighbour(SourceSlots.SuccOrder, SourceSlots.SuccCount, Stored.W);
            SourceSlots.OutEdges.Remove(Stored);
        }
    }

    public void RemoveEdge(string V, string W, string? Name = null)
    {
        RemoveEdge(new DagreEdge
        {
            V = V,
            W = W,
            Name = Name
        });
    }

    private void RemoveFromParentList(string V)
    {
        if (!_Parent.TryGetValue(V, out string? ParentId))
        {
            return;
        }

        if (_Children.TryGetValue(ParentId, out List<string>? Kids))
        {
            Kids.Remove(V);
        }
    }

    private string EdgeIdOf(string V, string W, string? Name, out string StoredV, out string StoredW, out string? StoredName)
    {
        StoredV = V;
        StoredW = W;
        if (!_IsDirected && string.CompareOrdinal(StoredV, StoredW) > 0)
        {
            string Swap = StoredV;
            StoredV = StoredW;
            StoredW = Swap;
        }

        StoredName = Name;
        string NamePart = Name ?? DEFAULT_EDGE_NAME;
        return StoredV + "\u0001" + StoredW + "\u0001" + NamePart;
    }

    private static void AddNeighbour(NodeSlots Slots, List<string> Order, Dictionary<string, int> Counts, string Neighbour)
    {
        if (Counts.TryGetValue(Neighbour, out int Count))
        {
            Counts[Neighbour] = Count + 1;
            return;
        }

        Counts[Neighbour] = 1;
        Order.Add(Neighbour);
    }

    private static void RemoveNeighbour(List<string> Order, Dictionary<string, int> Counts, string Neighbour)
    {
        if (!Counts.TryGetValue(Neighbour, out int Count))
        {
            return;
        }

        Count = Count - 1;
        if (Count == 0)
        {
            Counts.Remove(Neighbour);
            Order.Remove(Neighbour);
            return;
        }

        Counts[Neighbour] = Count;
    }

    private sealed class NodeSlots
    {
        public List<string> PredOrder { get; } = [];

        public Dictionary<string, int> PredCount { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        public List<string> SuccOrder { get; } = [];

        public Dictionary<string, int> SuccCount { get; } = new Dictionary<string, int>(StringComparer.Ordinal);

        public List<DagreEdge> InEdges { get; } = [];

        public List<DagreEdge> OutEdges { get; } = [];
    }
}
