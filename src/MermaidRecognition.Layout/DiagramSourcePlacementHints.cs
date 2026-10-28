using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

// A retained hint edit moves only its owner and existing relative-hint dependents.
// Occupied target cells reject the edit; automatic placement/compaction is never invoked.
internal static class DiagramSourcePlacementHints
{
    internal static string? Plan(DiagramContract Model, IReadOnlyDictionary<string, FlowchartCellRegion> Regions,
        IReadOnlyCollection<string> Changed, out Dictionary<string, FlowchartCellRegion> Targets,
        IReadOnlyDictionary<string, FlowchartCellRegion>? Restored = null)
    {
        var Result = new Dictionary<string, FlowchartCellRegion>(Regions, StringComparer.Ordinal);
        if (Restored != null)
            foreach (var Pair in Restored) Result[Pair.Key] = new(Pair.Value.StartRow, Pair.Value.StartColumn,
                Result[Pair.Key].RowSpan, Result[Pair.Key].ColumnSpan);
        Targets = Result;
        var Nodes = Model.Nodes.ToDictionary(Node => Node.Id, StringComparer.Ordinal);
        HashSet<string> Moving = new(Changed, StringComparer.Ordinal);
        Queue<string> Pending = new(Changed);
        var Dependents = Model.Nodes.Where(Node => Node.PlacementHint is { IsActive: true, Kind: not PlacementHintKind.Push, PeerId: not null })
            .ToLookup(Node => Node.PlacementHint!.PeerId!, Node => Node.Id, StringComparer.Ordinal);
        while (Pending.TryDequeue(out string? Id))
        {
            foreach (string Dependent in Dependents[Id]) { if (Moving.Add(Dependent)) { Pending.Enqueue(Dependent); } }
        }
        HashSet<string> Visiting = new(StringComparer.Ordinal), Done = new(StringComparer.Ordinal);
        string? Resolve(string Id)
        {
            if (Done.Contains(Id)) { return null; }
            if (!Visiting.Add(Id)) { return "The placement hints contain a dependency cycle."; }
            NodeContract Node = Nodes[Id];
            if (Node.PlacementHint is { IsActive: true } Hint)
            {
                if (Hint.Offset < 0 || Hint.ColumnOffset < 0) { return "A placement hint offset must be nonnegative."; }
                if (!Enum.IsDefined(Hint.Kind)) { return "The placement hint kind is unsupported."; }
                if (Hint.Kind == PlacementHintKind.At)
                {
                    var Region = Result[Id];
                    Result[Id] = new(Hint.CellRow, Hint.CellColumn, Region.RowSpan, Region.ColumnSpan);
                    Done.Add(Id); Visiting.Remove(Id); return null;
                }
                FlowchartCellRegion Owner = Result[Id], Peer;
                if (Hint.Kind == PlacementHintKind.Push)
                {
                    var Others = Nodes.Values.Where(Other => Other.Id != Id && Other.ParentGroupId == Node.ParentGroupId)
                        .Select(Other => Result[Other.Id]).ToArray();
                    if (Others.Length == 0) { Done.Add(Id); Visiting.Remove(Id); return null; }
                    Peer = new(Others.Min(Item => Item.StartRow), Others.Min(Item => Item.StartColumn),
                        Others.Max(Item => Item.EndRow) - Others.Min(Item => Item.StartRow),
                        Others.Max(Item => Item.EndColumn) - Others.Min(Item => Item.StartColumn));
                }
                else
                {
                    if (Hint.PeerId == null || !Result.ContainsKey(Hint.PeerId)) { return "The placement hint requires a placed node peer."; }
                    if (Moving.Contains(Hint.PeerId))
                    {
                        string? Failure = Resolve(Hint.PeerId);
                        if (Failure != null) { return Failure; }
                    }
                    Peer = Result[Hint.PeerId];
                }
                int Row = Owner.StartRow, Column = Owner.StartColumn;
                if (Hint.Kind == PlacementHintKind.Push)
                {
                    switch (Hint.Direction)
                    {
                        case LayoutHintDirection.North: Row = checked(Peer.StartRow - Owner.RowSpan); break;
                        case LayoutHintDirection.South: Row = Peer.EndRow; break;
                        case LayoutHintDirection.West: Column = checked(Peer.StartColumn - Owner.ColumnSpan); break;
                        case LayoutHintDirection.East: Column = Peer.EndColumn; break;
                        default: return "The push placement hint requires a direction.";
                    }
                }
                else
                {
                    switch (Hint.Kind)
                    {
                        case PlacementHintKind.Above: Row = checked(Peer.StartRow - Owner.RowSpan - Hint.Offset); Column = Peer.StartColumn; break;
                        case PlacementHintKind.Below: Row = checked(Peer.EndRow + Hint.Offset); Column = Peer.StartColumn; break;
                        case PlacementHintKind.LeftOf: Column = checked(Peer.StartColumn - Owner.ColumnSpan - Hint.Offset); Row = Peer.StartRow; break;
                        case PlacementHintKind.RightOf: Column = checked(Peer.EndColumn + Hint.Offset); Row = Peer.StartRow; break;
                    }
                }
                Result[Id] = Hint.IsDiagonal ? FlowchartPlacementHints.DiagonalTarget(Hint, Owner, Peer)
                    : new(Row, Column, Owner.RowSpan, Owner.ColumnSpan);
            }
            Done.Add(Id); Visiting.Remove(Id); return null;
        }
        try
        {
            foreach (string Id in Moving.OrderBy(Id => Id, StringComparer.Ordinal))
            {
                string? Failure = Resolve(Id);
                if (Failure != null) { return Failure; }
            }
        }
        catch (OverflowException) { return "The placement hint exceeds the supported cell coordinate range."; }
        return null;
    }
}
