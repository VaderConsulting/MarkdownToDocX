using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>Creates interior cell corridors for links that skip a semantic band, without splitting a solid frame.</summary>
internal static partial class EnterpriseBandPlacement
{
    /// <summary>Reserves free columns near the ends' barycentre before the first port allocation and route completion.</summary>
    /// <param name="Contract">Typed endpoints, declared links, hints and containment.</param>
    /// <param name="Nodes">Complete fitted cell-owned placement in page-down coordinates.</param>
    /// <param name="Context">Optional owning capacity diagnostics.</param>
    /// <returns>Whether an atomic, legal intermediate-band displacement created a corridor.</returns>
    internal static bool ReserveSkippingBandCorridors(DiagramContract Contract, List<DiagramGridLayout.GridNode> Nodes, FlowchartRenderContext? Context = null)
    {
        Dictionary<string, Classification> Classes = Contract.Nodes.Where(Node => Node.Metadata.ContainsKey("enterpriseElementType"))
            .ToDictionary(Node => Node.Id, Node => Classify(Enum.Parse<EnterpriseElementType>(((RawMetadata)Node.Metadata["enterpriseElementType"]).RawValue)), StringComparer.Ordinal);
        Dictionary<string, DiagramGridLayout.GridNode> ById = Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        Dictionary<string, GroupContract> Groups = Contract.Groups.ToDictionary(Group => Group.Id, StringComparer.Ordinal);
        FlowchartLogicalGrid Grid = Nodes[0].CellNode!.Grid;
        bool Changed = false;
        foreach (EdgeContract? Edge in Contract.Edges.Where(Edge => Classes.ContainsKey(Edge.SourceId) && Classes.ContainsKey(Edge.TargetId)))
        {
            Classification SourceClass = Classes[Edge.SourceId]; Classification TargetClass = Classes[Edge.TargetId];
            if (!SourceClass.Conventional || !TargetClass.Conventional
                || Math.Abs(SourceClass.Band - TargetClass.Band) <= 1)
            {
                continue;
            }

            int FirstBand = Math.Min(SourceClass.Band, TargetClass.Band), LastBand = Math.Max(SourceClass.Band, TargetClass.Band);
            DiagramGridLayout.GridNode[] Intermediate = [.. Nodes.Where(Node => Classes.TryGetValue(Node.Id, out Classification? Item) && Item.Band > FirstBand && Item.Band < LastBand)];
            if (Intermediate.Length == 0)
            {
                continue;
            }

            FlowchartLogicalGrid.Snapshot Before = Grid.Capture();
            FlowchartCellRegion Source = Before.Regions[Edge.SourceId]; FlowchartCellRegion Target = Before.Regions[Edge.TargetId];
            int Barycentre = (int)Math.Round((Source.StartColumn + (double)Target.StartColumn) / 2, MidpointRounding.AwayFromZero);
            int FirstRow = Intermediate.Min(Node => Before.Regions[Node.Id].StartRow), LastRow = Intermediate.Max(Node => Before.Regions[Node.Id].EndRow);
            int Left = Before.Regions.Values.Min(Region => Region.StartColumn), Right = Before.Regions.Values.Max(Region => Region.EndColumn);
            bool Found = false;
            foreach (int Column in Enumerable.Range(Left, Right - Left).OrderBy(Column => Math.Abs((long)Column - Barycentre)).ThenBy(Column => Column))
            {
                if (Free(Before, Column)) { ReportCapacity(Context, $"BAND_CORRIDOR|{Edge.Id}|column={Column}|status=already-free"); Found = true; break; }
                HashSet<string> Moving = Intermediate.Where(Node => Before.Regions[Node.Id].StartColumn <= Column && Before.Regions[Node.Id].EndColumn > Column)
                    .Select(Node => Node.Id).ToHashSet(StringComparer.Ordinal);
                List<DiagramGridLayout.GridFrame> Frames = DiagramGridLayout.ProjectCellFrames(Contract, Nodes, Before);
                (double X, double Y) Origin = Grid.Origin(new(FirstRow, Column, 1, 1), Before);
                double Bottom = Grid.Origin(new(LastRow - 1, Column, 1, 1), Before).Y + Grid.Configuration.CellHeight;
                foreach (DiagramGridLayout.GridFrame? Frame in Frames.Where(Frame => !Member(Edge.SourceId, Frame.Id) && !Member(Edge.TargetId, Frame.Id)
                    && Frame.Left < Origin.X + Grid.Configuration.CellWidth && Frame.Right > Origin.X
                    && Math.Min(Frame.Top, Frame.Bottom) < Bottom && Math.Max(Frame.Top, Frame.Bottom) > Origin.Y))
                {
                    Moving.UnionWith(Nodes.Where(Node => Member(Node.Id, Frame.Id)).Select(Node => Node.Id));
                }

                if (Moving.Count == 0)
                {
                    continue;
                }

                FlowchartCellRepair.Assignment? Plan = null;
                for (int Depth = 0; Depth < FlowchartLayoutRules.RepairChainDepth; Depth++)
                {
                    // A frame moves as a complete member set. No corridor is
                    // carved through its rectangle by separating its band parts.
                    foreach (string Id in Moving.ToArray())
                    {
                        if (ById[Id].GroupId is { } Parent)
                        {
                            Moving.UnionWith(Nodes.Where(Node => Member(Node.Id, Parent)).Select(Node => Node.Id));
                        }
                    }

                    if (Moving.Contains(Edge.SourceId) || Moving.Contains(Edge.TargetId) || Moving.Any(Id => ById[Id].PlacementHintLocked))
                    {
                        break;
                    }

                    int Shift = Math.Max(1, Column + 1 - Moving.Min(Id => Before.Regions[Id].StartColumn));
                    Dictionary<string, FlowchartCellRegion> Changes = Moving.ToDictionary(Id => Id, Id => FlowchartCellRepair.Translate(Before.Regions[Id], 0, Shift), StringComparer.Ordinal);
                    DiagramGridLayout.GridNode[] Displaced = [.. Nodes.Where(Node => !Moving.Contains(Node.Id) && Changes.Values.Any(Region => Region.Overlaps(Before.Regions[Node.Id])))];
                    if (Displaced.Length > 0) { Moving.UnionWith(Displaced.Select(Node => Node.Id)); continue; }
                    Plan = FlowchartCellRepair.PlanAssignment(Nodes, Changes);
                    break;
                }
                if (Plan == null || !Free(Plan.After, Column) || !DiagramGridLayout.CellBandFramesAreLegal(Contract, Nodes, Plan.After))
                {
                    continue;
                }

                if (!Plan.TryApply())
                {
                    continue;
                }

                ReportCapacity(Context, $"BAND_CORRIDOR|{Edge.Id}|column={Column}|status=reserved|nodes={string.Join(",", Plan.Nodes.Select(Node => Node.Id))}");
                Changed = Found = true; break;
            }
            if (!Found)
            {
                ReportCapacity(Context, $"BAND_CORRIDOR|{Edge.Id}|status=blocked-by-fixed-regions-or-solid-frames");
            }

            // Validates a complete proposed free strip against cells and the
            // exact forward-projected frame bounds.
            bool Free(FlowchartLogicalGrid.Snapshot State, int Column)
            {
                if (State.Regions.Values.Any(Region => Region.StartColumn <= Column && Region.EndColumn > Column
                    && Region.StartRow < LastRow && Region.EndRow > FirstRow))
                {
                    return false;
                }

                (double X, double Y) Origin = Grid.Origin(new(FirstRow, Column, 1, 1), State);
                double Bottom = Grid.Origin(new(LastRow - 1, Column, 1, 1), State).Y + Grid.Configuration.CellHeight;
                return DiagramGridLayout.ProjectCellFrames(Contract, Nodes, State).All(Frame => Member(Edge.SourceId, Frame.Id) || Member(Edge.TargetId, Frame.Id)
                    || Frame.Left >= Origin.X + Grid.Configuration.CellWidth || Frame.Right <= Origin.X
                    || Math.Min(Frame.Top, Frame.Bottom) >= Bottom || Math.Max(Frame.Top, Frame.Bottom) <= Origin.Y);
            }
        }
        return Changed;

        // Checks declared ancestry without converting frame bounds into cell coordinates.
        bool Member(string Id, string GroupId)
        {
            string? Parent = ById.GetValueOrDefault(Id)?.GroupId ?? Groups.GetValueOrDefault(Id)?.ParentGroupId;
            HashSet<string> Seen = new HashSet<string>(StringComparer.Ordinal);
            while (Parent != null && Seen.Add(Parent))
            { if (Parent == GroupId) { return true; } Parent = Groups.GetValueOrDefault(Parent)?.ParentGroupId; }
            return Id == GroupId;
        }
    }
}
