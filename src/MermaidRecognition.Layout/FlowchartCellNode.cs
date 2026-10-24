using System;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>A node view whose position and size are derived exclusively from its occupied cell region.</summary>
internal sealed class FlowchartCellNode
{
    /// <summary>Creates a view of an existing reservation without inspecting or converting pixel bounds.</summary>
    /// <param name="Grid">The owning diagram's authoritative cell state.</param>
    /// <param name="NodeId">The node whose reservation already exists.</param>
    /// <param name="Diamond">Whether the drawn diamond is centred in, rather than fills, its reservation.</param>
    internal FlowchartCellNode(FlowchartLogicalGrid Grid, string NodeId, bool Diamond)
    {
        ArgumentNullException.ThrowIfNull(Grid);
        ArgumentException.ThrowIfNullOrEmpty(NodeId);
        _ = Grid.RegionOf(NodeId);
        this.Grid = Grid;
        this.NodeId = NodeId;
        this.Diamond = Diamond;
        if (Diamond) Grid.Require(NodeId, Region => Grid.Measure(Region).Width >= Grid.Configuration.CellWidth
            && Grid.Measure(Region).Height >= Grid.Configuration.CellHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR);
    }

    /// <summary>The only mutable source of node occupancy and derived extents.</summary>
    internal FlowchartLogicalGrid Grid { get; }
    /// <summary>Identity of the single node occupying this region.</summary>
    internal string NodeId { get; }
    /// <summary>Whether the visible silhouette is a proportion-preserving diamond.</summary>
    private bool Diamond { get; }
    /// <summary>The exact occupied rectangle; it is never inferred from the view's centre.</summary>
    internal FlowchartCellRegion Region => Grid.RegionOf(NodeId);
    /// <summary>Horizontal centre of the reserved rectangle in page coordinates.</summary>
    internal double X => Grid.Origin(Region).X + Grid.Measure(Region).Width / 2;
    /// <summary>Vertical centre of the reserved rectangle in page-down coordinates.</summary>
    internal double Y => RoutingFlip is { } Flip ? Flip - PageY : PageY;
    /// <summary>Page-down centre before the existing routing coordinate adapter.</summary>
    private double PageY => Grid.Origin(Region).Y + Grid.Measure(Region).Height / 2;
    /// <summary>Shared reflection origin supplied by the existing coordinate boundary, never inferred from a node position.</summary>
    private double? RoutingFlip { get; set; }
    /// <summary>Identifies the existing view boundary so frame fitting uses the same axis as its members.</summary>
    internal bool UsesRoutingCoordinates => RoutingFlip.HasValue;
    /// <summary>Explicit shared reflection origin retained at the coordinate boundary, never recovered from drawing positions.</summary>
    internal double? RoutingOrigin => RoutingFlip;

    /// <summary>Projects the same unchanged region into the existing routing coordinate frame.</summary>
    /// <param name="Flip">Diagram-wide reflection origin computed by the established coordinate adapter.</param>
    internal void UseRoutingCoordinates(double Flip)
    {
        if (!double.IsFinite(Flip)) throw new ArgumentOutOfRangeException(nameof(Flip));
        RoutingFlip = Flip;
    }
    /// <summary>Visible width, including internal gaps for a rectangular node.</summary>
    internal double Width => Diamond ? DiamondScale * Grid.Configuration.CellWidth : Grid.Measure(Region).Width;
    /// <summary>Visible height, including internal gaps for a rectangular node.</summary>
    internal double Height => Diamond ? DiamondScale * Grid.Configuration.CellHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR
        : Grid.Measure(Region).Height;
    /// <summary>Shape scale selected by the declared spans; growing an internal routing gap never enlarges the diamond merely to fill it.</summary>
    private double DiamondScale => ScaleFor(Region);

    /// <summary>Measures the proportional silhouette for a declared region, without changing the live reservation.</summary>
    /// <param name="Region">Complete proposed rectangle.</param>
    /// <returns>Whole base silhouette scale for a diamond.</returns>
    private double ScaleFor(FlowchartCellRegion Region) => Math.Floor(Math.Min(
        (Region.ColumnSpan * Grid.Configuration.CellWidth + (Region.ColumnSpan - 1.0) * Grid.Configuration.ColumnGap) / Grid.Configuration.CellWidth,
        (Region.RowSpan * Grid.Configuration.CellHeight + (Region.RowSpan - 1.0) * Grid.Configuration.RowGap)
            / (Grid.Configuration.CellHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR)));

    /// <summary>Builds the current renderer-facing bounds without retaining any independent pixel position or size.</summary>
    /// <param name="Provider">Optional renderer-owned connection and obstacle geometry.</param>
    /// <returns>The geometry derived from the current complete cell state.</returns>
    internal FlowchartLayoutNodeBounds Bounds(INodeGeometryProvider? Provider = null) => Bounds(Region, Provider);

    /// <summary>Projects a complete target region for clearance planning without publishing the candidate.</summary>
    /// <param name="Target">Explicit target rectangle; it is not inferred from drawing coordinates.</param>
    /// <param name="Provider">Optional renderer-owned local geometry.</param>
    /// <param name="State">Optional prepared ownership and gaps, projected without live mutation.</param>
    /// <param name="PageCoordinates">Whether a frame builder needs the original page-down projection before applying the shared reflection once.</param>
    /// <returns>Candidate drawing bounds in the existing coordinate view.</returns>
    internal FlowchartLayoutNodeBounds Bounds(FlowchartCellRegion Target, INodeGeometryProvider? Provider = null, FlowchartLogicalGrid.Snapshot? State = null,
        bool PageCoordinates = false)
    {
        var Origin = Grid.Origin(Target, State); var Size = Grid.Measure(Target, State);
        double PageCentre = Origin.Y + Size.Height / 2;
        double W = Diamond ? ScaleFor(Target) * Grid.Configuration.CellWidth : Size.Width;
        double H = Diamond ? ScaleFor(Target) * Grid.Configuration.CellHeight * FlowchartDagrePresentation.DIAMOND_HEIGHT_FACTOR : Size.Height;
        return new()
        {
            NodeId = NodeId, CenterX = Origin.X + Size.Width / 2,
            CenterY = !PageCoordinates && RoutingFlip is { } Flip ? Flip - PageCentre : PageCentre,
            Width = W, Height = H, Geometry = Provider?.Create(W, H)
        };
    }
}
