using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using MermaidRecognition.Contract;
using MermaidRecognition.Render;

namespace MermaidRecognition.Layout;

/// <summary>Model geometry for the reserved title strip along each group's top edge.</summary>
internal static class FlowchartFrameTitleBandHelper
{
    internal readonly record struct Band(string GroupId, double Left, double Bottom, double Right, double Top);

    private sealed class CachedBands
    {
        public required IReadOnlyList<Band> Items { get; init; }
    }

    private static readonly ConditionalWeakTable<IReadOnlyDictionary<string, FlowchartLayoutNodeBounds>, CachedBands>
        Cache = new();

    internal static IReadOnlyList<Band> Get(DiagramContract contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds) =>
        Cache.GetValue(bounds, _ => new CachedBands { Items = Build(contract, bounds) }).Items;

    internal static IReadOnlyList<Band> Build(DiagramContract contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds)
    {
        FlowchartRenderOptions options = FlowchartRenderOptions.FromContract(contract);
        Dictionary<string, GroupContract> groups = contract.Groups.ToDictionary(group => group.Id, StringComparer.Ordinal);
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> effectiveBounds = WithComputedGroupBounds(
            contract, bounds, options);
        List<Band> result = [];
        foreach (FlowchartLayoutNodeBounds frame in effectiveBounds.Values)
        {
            if (!groups.TryGetValue(frame.NodeId, out GroupContract? group)) continue;
            string title = string.IsNullOrWhiteSpace(group.Title) ? group.Id : group.Title;
            double strip = DiagramGridMetrics.CeilToGrid(Math.Max(DiagramGridMetrics.GRID_UNIT_PX,
                FlowchartSubgraphSvgRenderer.RequiredTitleBandPx(options.FontSizePx, title.Split('\n').Length)));
            double halfWidth = frame.Width / 2.0;
            double halfHeight = frame.Height / 2.0;
            if (halfWidth <= 0 || halfHeight <= 0) continue;
            double top = frame.CenterY + halfHeight;
            result.Add(new Band(group.Id, frame.CenterX - halfWidth,
                top - strip, frame.CenterX + halfWidth, top));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> WithComputedGroupBounds(
        DiagramContract contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds,
        FlowchartRenderOptions options)
    {
        HashSet<string> invalidFrames = contract.Groups
            .Where(group => !bounds.TryGetValue(group.Id, out FlowchartLayoutNodeBounds frame)
                || frame.Width <= 0 || frame.Height <= 0)
            .Select(group => group.Id).ToHashSet(StringComparer.Ordinal);
        if (invalidFrames.Count == 0) return bounds;

        Dictionary<string, FlowchartLayoutNodeBounds> computed = new(bounds, StringComparer.Ordinal);
        FlowchartSubgraphBoundsRequest request = FlowchartSubgraphBoundsRequest.FromContract(contract, options);
        foreach (FlowchartSubgraphLayoutRect rect in FlowchartSubgraphBoundsBuilder.BuildOrderedRects(
                     contract, bounds, request))
        {
            if (!invalidFrames.Contains(rect.GroupId)) continue;
            computed[rect.GroupId] = new FlowchartLayoutNodeBounds
            {
                NodeId = rect.GroupId,
                CenterX = (rect.Left + rect.Right) / 2.0,
                CenterY = (rect.Bottom + rect.Top) / 2.0,
                Width = rect.Right - rect.Left,
                Height = rect.Top - rect.Bottom
            };
        }
        return computed;
    }

    /// <summary>True only when an orthogonal segment enters the open interior of the band.</summary>
    internal static bool SegmentEnters(Band band, double x1, double y1, double x2, double y2)
    {
        const double epsilon = 0.5;
        if (Math.Abs(y1 - y2) <= epsilon)
        {
            if (y1 <= band.Bottom + epsilon || y1 >= band.Top - epsilon) return false;
            return Math.Min(Math.Max(x1, x2), band.Right - epsilon)
                > Math.Max(Math.Min(x1, x2), band.Left + epsilon);
        }
        if (Math.Abs(x1 - x2) <= epsilon)
        {
            if (x1 <= band.Left + epsilon || x1 >= band.Right - epsilon) return false;
            return Math.Min(Math.Max(y1, y2), band.Top - epsilon)
                > Math.Max(Math.Min(y1, y2), band.Bottom + epsilon);
        }
        // The layout model requires orthogonal paths. Treat any unexpected diagonal
        // through a band as an intersection as well, so it cannot silently pass.
        double minX = Math.Min(x1, x2), maxX = Math.Max(x1, x2);
        double minY = Math.Min(y1, y2), maxY = Math.Max(y1, y2);
        return maxX > band.Left + epsilon && minX < band.Right - epsilon
            && maxY > band.Bottom + epsilon && minY < band.Top - epsilon;
    }

    internal static bool RouteEntersAny(IReadOnlyList<(double X, double Y)> points,
        IReadOnlyList<Band> bands, string sourceId, string targetId, DiagramContract contract)
        => FindEnteredBandIds(points, bands, sourceId, targetId, contract).Count > 0;

    internal static IReadOnlyList<string> FindEnteredBandIds(IReadOnlyList<(double X, double Y)> points,
        IReadOnlyList<Band> bands, string sourceId, string targetId, DiagramContract contract)
    {
        HashSet<string> entered = new(StringComparer.Ordinal);
        for (int index = 1; index < points.Count; index++)
        {
            (double x1, double y1) = points[index - 1];
            (double x2, double y2) = points[index];
            foreach (Band band in bands)
            {
                // A route connecting a member to a non-member may cross the group's
                // top outline at a port. Only the one vertical segment that crosses
                // that outline is exempt; travel along or through the title strip is not.
                if (IsLegalTopPortCrossing(band, x1, y1, x2, y2, sourceId, targetId, contract)) continue;
                if (SegmentEnters(band, x1, y1, x2, y2)) entered.Add(band.GroupId);
            }
        }
        return entered.OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }

    internal static bool IsLegalTopPortCrossing(Band band, double x1, double y1, double x2, double y2,
        string sourceId, string targetId, DiagramContract contract)
    {
        bool sourceInside = DiagramGridLayout.IsInsideGroup(contract, sourceId, band.GroupId);
        bool targetInside = DiagramGridLayout.IsInsideGroup(contract, targetId, band.GroupId);
        bool crossesTop = (y1 - band.Top) * (y2 - band.Top) < 0;
        bool startsAtTopPort = Math.Abs(y1 - band.Top) <= 0.5 && y2 < band.Top - 0.5;
        bool endsAtTopPort = Math.Abs(y2 - band.Top) <= 0.5 && y1 < band.Top - 0.5;
        return sourceInside != targetInside
            && Math.Abs(x1 - x2) <= 0.5
            && x1 > band.Left + 0.5 && x1 < band.Right - 0.5
            && (crossesTop || startsAtTopPort || endsAtTopPort)
            && SegmentEnters(band, x1, y1, x2, y2);
    }

    internal static Dictionary<string, FlowchartLayoutNodeBounds> AddRoutingObstacles(DiagramContract contract,
        IReadOnlyDictionary<string, FlowchartLayoutNodeBounds> bounds)
    {
        Dictionary<string, FlowchartLayoutNodeBounds> result = new(bounds, StringComparer.Ordinal);
        foreach (Band band in Build(contract, bounds))
        {
            string id = ObstacleId(band.GroupId);
            result[id] = new FlowchartLayoutNodeBounds
            {
                NodeId = id,
                CenterX = (band.Left + band.Right) / 2.0,
                CenterY = (band.Bottom + band.Top) / 2.0,
                Width = band.Right - band.Left,
                Height = band.Top - band.Bottom
            };
        }
        return result;
    }

    internal static bool TryGetObstacleBand(FlowchartLayoutNodeBounds bounds, out Band band)
    {
        const string prefix = "\0title-band:";
        if (bounds.NodeId.StartsWith(prefix, StringComparison.Ordinal))
        {
            band = new Band(bounds.NodeId[prefix.Length..],
                bounds.CenterX - bounds.Width / 2.0, bounds.CenterY - bounds.Height / 2.0,
                bounds.CenterX + bounds.Width / 2.0, bounds.CenterY + bounds.Height / 2.0);
            return true;
        }
        band = default;
        return false;
    }

    private static string ObstacleId(string groupId) => "\0title-band:" + groupId;
}
