using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Hashing;
using System.Linq;
using System.Text;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>Deterministic 64-bit digests for layout cache keys without JSON allocation.</summary>
internal static class FlowchartLayoutGeometryHash
{
    internal static ulong HashLayoutGeometry(FlowchartLayoutResult Layout)
    {
        XxHash64 Hasher = new XxHash64();
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds.OrderBy(Box => Box.NodeId, StringComparer.Ordinal))
        {
            AppendLayoutBounds(Hasher, [Box]);
        }

        foreach (FlowchartLayoutNodeBounds Box in Layout.ClusterBounds.OrderBy(Box => Box.NodeId, StringComparer.Ordinal))
        {
            AppendLayoutBounds(Hasher, [Box]);
        }

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes.OrderBy(Route => Route.EdgeId, StringComparer.Ordinal))
        {
            AppendString(Hasher, Route.EdgeId);
            AppendString(Hasher, Route.SourceId);
            AppendString(Hasher, Route.TargetId);
            AppendString(Hasher, Route.ExitSide);
            AppendString(Hasher, Route.EntrySide);
            AppendDouble(Hasher, Route.ExitNormalX);
            AppendDouble(Hasher, Route.ExitNormalY);
            AppendDouble(Hasher, Route.EntryNormalX);
            AppendDouble(Hasher, Route.EntryNormalY);
            Hasher.Append(BitConverter.GetBytes(Route.Points.Count));
            foreach (FlowchartLayoutPoint Point in Route.Points)
            {
                AppendDouble(Hasher, Point.X);
                AppendDouble(Hasher, Point.Y);
            }
        }

        return Hasher.GetCurrentHashAsUInt64();
    }

    internal static ulong HashBounds(IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> Bounds)
    {
        XxHash64 Hasher = new XxHash64();
        foreach (KeyValuePair<string, FlowchartLayoutNodeBounds> Pair in Bounds.OrderBy(Item => Item.Key, StringComparer.Ordinal))
        {
            AppendString(Hasher, Pair.Key);
            AppendDouble(Hasher, Pair.Value.CenterX);
            AppendDouble(Hasher, Pair.Value.CenterY);
            AppendDouble(Hasher, Pair.Value.Width);
            AppendDouble(Hasher, Pair.Value.Height);
        }

        return Hasher.GetCurrentHashAsUInt64();
    }

    internal static ulong HashCellRepairSignature(FlowchartLogicalGrid.Snapshot CellState)
    {
        XxHash64 Hasher = new XxHash64();
        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in CellState.Regions.OrderBy(Item => Item.Key, StringComparer.Ordinal))
        {
            AppendString(Hasher, Pair.Key);
            Hasher.Append(BitConverter.GetBytes(Pair.Value.StartRow));
            Hasher.Append(BitConverter.GetBytes(Pair.Value.StartColumn));
            Hasher.Append(BitConverter.GetBytes(Pair.Value.RowSpan));
            Hasher.Append(BitConverter.GetBytes(Pair.Value.ColumnSpan));
        }

        AppendGapDictionary(Hasher, CellState.ColumnGaps);
        AppendGapDictionary(Hasher, CellState.RowGaps);
        return Hasher.GetCurrentHashAsUInt64();
    }

    internal static ulong HashCellAssignmentIdentity(FlowchartCellRepair.Assignment Assignment)
    {
        XxHash64 Hasher = new XxHash64();
        foreach (KeyValuePair<string, FlowchartCellRegion> Pair in Assignment.After.Regions
                     .Where(Item => !Assignment.Before.Regions.TryGetValue(Item.Key, out FlowchartCellRegion? Old) || Old != Item.Value)
                     .OrderBy(Item => Item.Key, StringComparer.Ordinal))
        {
            AppendString(Hasher, Pair.Key);
            Hasher.Append(BitConverter.GetBytes(Pair.Value.StartRow));
            Hasher.Append(BitConverter.GetBytes(Pair.Value.StartColumn));
            Hasher.Append(BitConverter.GetBytes(Pair.Value.RowSpan));
            Hasher.Append(BitConverter.GetBytes(Pair.Value.ColumnSpan));
        }

        AppendGapDictionary(Hasher, Assignment.After.ColumnGaps);
        AppendGapDictionary(Hasher, Assignment.After.RowGaps);
        return Hasher.GetCurrentHashAsUInt64();
    }

    private static void AppendGapDictionary(XxHash64 Hasher, IReadOnlyDictionary<int, double> Gaps)
    {
        foreach (KeyValuePair<int, double> Pair in Gaps.OrderBy(Item => Item.Key))
        {
            Hasher.Append(BitConverter.GetBytes(Pair.Key));
            AppendDouble(Hasher, Pair.Value);
        }
    }

    internal static ulong HashFacingPathKey(
        FlowchartLayoutEdgeRoute Changed,
        string? SourceShape,
        string? TargetShape,
        FlowchartDirection Direction,
        bool Horizontal,
        LayoutHintDirection? Travel,
        bool? HintActive)
    {
        XxHash64 Hasher = new XxHash64();
        AppendString(Hasher, Changed.SourceId);
        AppendString(Hasher, Changed.TargetId);
        AppendString(Hasher, Changed.ExitSide);
        AppendString(Hasher, Changed.EntrySide);
        AppendDouble(Hasher, Changed.Points[0].X);
        AppendDouble(Hasher, Changed.Points[0].Y);
        AppendDouble(Hasher, Changed.Points[^1].X);
        AppendDouble(Hasher, Changed.Points[^1].Y);
        AppendDouble(Hasher, Changed.ExitNormalX);
        AppendDouble(Hasher, Changed.ExitNormalY);
        AppendDouble(Hasher, Changed.EntryNormalX);
        AppendDouble(Hasher, Changed.EntryNormalY);
        AppendString(Hasher, SourceShape ?? string.Empty);
        AppendString(Hasher, TargetShape ?? string.Empty);
        Hasher.Append(BitConverter.GetBytes((int)Direction));
        Hasher.Append(BitConverter.GetBytes(Horizontal));
        Hasher.Append(BitConverter.GetBytes(Travel.HasValue ? (int)Travel.Value : int.MinValue));
        Hasher.Append(BitConverter.GetBytes(HintActive == true));
        return Hasher.GetCurrentHashAsUInt64();
    }

    internal static string FormatKey(ulong Digest) => Digest.ToString("X16", CultureInfo.InvariantCulture);

    private static void AppendLayoutBounds(XxHash64 Hasher, IReadOnlyList<FlowchartLayoutNodeBounds> Bounds)
    {
        foreach (FlowchartLayoutNodeBounds Box in Bounds.OrderBy(Box => Box.NodeId, StringComparer.Ordinal))
        {
            AppendString(Hasher, Box.NodeId);
            AppendDouble(Hasher, Box.CenterX);
            AppendDouble(Hasher, Box.CenterY);
            AppendDouble(Hasher, Box.Width);
            AppendDouble(Hasher, Box.Height);
        }
    }

    private static void AppendString(XxHash64 Hasher, string Value)
    {
        byte[] Bytes = Encoding.UTF8.GetBytes(Value);
        Hasher.Append(Bytes);
    }

    private static void AppendDouble(XxHash64 Hasher, double Value)
    {
        Hasher.Append(BitConverter.GetBytes(Value));
    }
}
