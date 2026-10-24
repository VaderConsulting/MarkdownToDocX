using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Reserves measured routing capacity before bands are exposed to the router.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>Reports measured band and wall capacity independently of routing or checker output.</summary>
    internal static Action<string>? CapacityObserver;

    /// <summary>Grows only deficient external band gaps, committing their measured increases together.</summary>
    /// <param name="Contract">Declared relationships and containment.</param>
    /// <param name="Nodes">Complete production cell ownership in page-down coordinates.</param>
    /// <param name="Context">Optional owning diagnostics; no global render context is borrowed.</param>
    /// <returns>Whether one complete measured gap-growth assignment was accepted.</returns>
    internal static bool ReserveRoutingSpace(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartRenderContext? Context = null)
    {
        var Classes = Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
            .ToDictionary(Node => Node.Id, Node => Classify(Enum.Parse<MermaidRecognition.Kinds.Enterprise.EnterpriseElementType>(
                ((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)), StringComparer.Ordinal);
        var Bands = Classes.Where(Pair => Pair.Value.Conventional)
            .GroupBy(Pair => Pair.Value.Band).OrderBy(Group => Group.Key).ToArray();
        var Grid = Nodes[0].CellNode!.Grid;
        var Regions = Grid.Capture().Regions;
        var Growth = new List<FlowchartCellGapGrowth>();
        for (int Index = 1; Index < Bands.Length; Index++)
        {
            int FirstBand = Bands[Index - 1].Key, NextBand = Bands[Index].Key;
            var Lines = Contract.Edges.Where(Edge => Crosses(Edge, FirstBand, NextBand)).ToArray();
            int End = Bands[Index - 1].Max(Pair => Regions[Pair.Key].EndRow);
            int Start = Bands[Index].Min(Pair => Regions[Pair.Key].StartRow);
            double Available = Grid.Origin(new(Start, 0, 1, 1)).Y
                - Grid.Origin(new(End - 1, 0, 1, 1)).Y - Grid.Configuration.CellHeight;
            double Required = Lines.Length == 0 ? 0 : 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX
                + (Lines.Length - 1) * FlowchartLayoutRules.TrackSpacing;
            double Shortfall = Math.Max(0, Required - Available);
            ReportCapacity(Context, $"BAND_CAPACITY|{FirstBand}->{NextBand}|lines={Lines.Length}|capacity={Math.Max(0, (int)Math.Floor((Available - 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX) / FlowchartLayoutRules.TrackSpacing) + 1)}|available={Available:R}|required={Required:R}|shortfall={Shortfall:R}|routes={string.Join(",", Lines.Select(Line => Line.Id))}");
            if (Shortfall > 0) Growth.Add(new(false, End - 1, Shortfall, DiagramGridMetrics.GRID_UNIT_PX));
        }
        var Frames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Grid.Capture());
        foreach (var Frame in Frames)
        {
            var LayerRange = Range(Frame.Id);
            if (LayerRange == null || LayerRange.Value.First == LayerRange.Value.Last) continue;
            var Lines = Contract.Edges.Where(Edge => !MemberId(Edge.SourceId, Frame.Id) && !MemberId(Edge.TargetId, Frame.Id)
                && Bands.Zip(Bands.Skip(1), (A, B) => (A.Key, BKey: B.Key)).Any(Pair => LayerRange.Value.First <= Pair.Key
                    && LayerRange.Value.Last >= Pair.BKey && Crosses(Edge, Pair.Key, Pair.BKey))).ToArray();
            if (Lines.Length == 0) continue;
            var Members = Nodes.Where(Node => MemberId(Node.Id, Frame.Id)).ToArray();
            var Foreign = Nodes.Where(Node => !MemberId(Node.Id, Frame.Id)).Select(Node =>
            {
                var Region = Regions[Node.Id]; var Origin = Grid.Origin(Region); var Size = Grid.Measure(Region);
                return (Origin.X, Origin.Y, Size.Width, Size.Height);
            }).Where(Box => Box.Y < Math.Max(Frame.Top, Frame.Bottom) && Box.Y + Box.Height > Math.Min(Frame.Top, Frame.Bottom)).ToArray();
            double Required = 2 * DiagramGridRouteBuilder.CORNER_RADIUS_PX + (Lines.Length - 1) * FlowchartLayoutRules.TrackSpacing;
            var Options = new[]
            {
                (Side: "left", After: Members.Min(Node => Regions[Node.Id].StartColumn) - 1,
                    Available: Foreign.Where(Box => Box.X + Box.Width <= Frame.Left).Select(Box => Frame.Left - Box.X - Box.Width).DefaultIfEmpty(double.PositiveInfinity).Min()),
                (Side: "right", After: Members.Max(Node => Regions[Node.Id].EndColumn) - 1,
                    Available: Foreign.Where(Box => Box.X >= Frame.Right).Select(Box => Box.X - Frame.Right).DefaultIfEmpty(double.PositiveInfinity).Min())
            }.Where(Option => !Regions.Values.Any(Region => Region.StartColumn <= Option.After && Option.After < Region.EndColumn - 1))
                .OrderBy(Option => Math.Max(0, Required - Option.Available)).ThenBy(Option => Option.Side, StringComparer.Ordinal).ToArray();
            if (Options.Length == 0) { ReportCapacity(Context, $"BAND_WALL_CAPACITY|{Frame.Id}|lines={Lines.Length}|status=no-external-gap"); continue; }
            var Best = Options[0]; double Shortfall = Math.Max(0, Required - Best.Available);
            ReportCapacity(Context, $"BAND_WALL_CAPACITY|{Frame.Id}|lines={Lines.Length}|side={Best.Side}|available={Best.Available:R}|required={Required:R}|shortfall={Shortfall:R}");
            if (Shortfall > 0) Growth.Add(new(true, Best.After, Shortfall, DiagramGridMetrics.GRID_UNIT_PX));
        }
        Growth = Growth.GroupBy(Item => (Item.Horizontal, Item.After)).Select(Group => Group.OrderByDescending(Item => Item.Shortfall).First()).ToList();
        return Growth.Count > 0 && FlowchartCellRepair.PlanAssignment(Nodes,
            new Dictionary<string, FlowchartCellRegion>(), Growth)?.TryApply() == true;

        /// <summary>Tests whether declared endpoints lie on opposite sides of this band boundary.</summary>
        bool Crosses(EdgeContract Edge, int First, int Next)
        {
            var A = Range(Edge.SourceId); var B = Range(Edge.TargetId);
            return A != null && B != null && (A.Value.Last <= First && B.Value.First >= Next
                || B.Value.Last <= First && A.Value.First >= Next);
        }

        /// <summary>Resolves the semantic range of a node or a solid frame from its typed descendants.</summary>
        (int First, int Last)? Range(string Id)
        {
            if (Classes.TryGetValue(Id, out var Item) && Item.Conventional) return (Item.Band, Item.Band);
            var Members = Contract.Nodes.Where(Node => IsMember(Node.ParentGroupId, Id)).Select(Node => Classes.GetValueOrDefault(Node.Id))
                .Where(Item => Item is { Conventional: true }).ToArray();
            return Members.Length == 0 ? null : (Members.Min(Item => Item!.Band), Members.Max(Item => Item!.Band));
        }

        /// <summary>Follows declared ancestry; no frame pixels are converted back into cell ownership.</summary>
        bool IsMember(string? Parent, string Id)
        {
            var Seen = new HashSet<string>(StringComparer.Ordinal);
            while (Parent != null && Seen.Add(Parent))
            {
                if (Parent == Id) return true;
                Parent = Contract.Groups.FirstOrDefault(Group => Group.Id == Parent)?.ParentGroupId;
            }
            return false;
        }

        /// <summary>Includes frame identities themselves when comparing solid walls with relationship endpoints.</summary>
        bool MemberId(string Id, string FrameId) => Id == FrameId || IsMember(
            Contract.Nodes.FirstOrDefault(Node => Node.Id == Id)?.ParentGroupId ?? Contract.Groups.FirstOrDefault(Group => Group.Id == Id)?.ParentGroupId, FrameId);
    }

    /// <summary>Retains a measured placement fact in the owning diagram and optional diagnostic observer.</summary>
    /// <param name="Context">Owning diagram, absent for pure placement unit tests.</param>
    /// <param name="Message">Deterministic measured capacity or accepted corridor.</param>
    private static void ReportCapacity(FlowchartRenderContext? Context, string Message)
    {
        Context?.BandSpaceDiagnostics.Add(Message);
        CapacityObserver?.Invoke(Message);
    }
}
