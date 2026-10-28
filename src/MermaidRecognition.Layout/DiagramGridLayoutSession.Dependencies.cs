using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

public static partial class DiagramGridLayout
{
    /// <summary>Immutable geometry values plus references owned exclusively by one private candidate.</summary>
    private sealed class RetainedRouteSnapshot
    {
        private readonly DiagramContract Contract;
        private readonly Dictionary<string, FlowchartLayoutNodeBounds> Bounds;
        private readonly RouteValue[] Routes;

        internal RetainedRouteSnapshot(GridLayoutState State)
        {
            Contract = State.Contract;
            Bounds = State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            Routes = State.Copy.EdgeRoutes.Select(Route => new RouteValue(Route)).ToArray();
        }

        internal HashSet<FlowchartLayoutEdgeRoute> RoutesAffectedBy(GridLayoutState State)
        {
            var NewBounds = State.Copy.NodeBounds.Concat(State.Copy.ClusterBounds).ToDictionary(Box => Box.NodeId, StringComparer.Ordinal);
            HashSet<string> Changed = Bounds.Keys.Union(NewBounds.Keys).Where(Id => !Bounds.TryGetValue(Id, out var Old)
                || !NewBounds.TryGetValue(Id, out var Current) || (Old.CenterX, Old.CenterY, Old.Width, Old.Height)
                    != (Current.CenterX, Current.CenterY, Current.Width, Current.Height)).ToHashSet(StringComparer.Ordinal);
            List<FlowchartLayoutNodeBounds> Obstacles = [.. Bounds.Values.Concat(NewBounds.Values).Where(Box => Changed.Contains(Box.NodeId))];
            foreach (var Band in FlowchartFrameTitleBandHelper.Build(Contract, Bounds)
                .Concat(FlowchartFrameTitleBandHelper.Build(State.Contract, NewBounds)).Where(Band => Changed.Contains(Band.GroupId)))
            {
                Obstacles.Add(new() { NodeId = Band.GroupId, CenterX = (Band.Left + Band.Right) / 2,
                    CenterY = (Band.Bottom + Band.Top) / 2, Width = Band.Right - Band.Left, Height = Band.Top - Band.Bottom });
            }
            double Margin = DiagramGridRouteBuilder.CORNER_RADIUS_PX;
            return [.. Routes.Where(Item => Changed.Contains(Item.Source) || Changed.Contains(Item.Target)
                || Enumerable.Range(1, Math.Max(0, Item.Points.Length - 1)).Any(Index => Obstacles.Any(Box =>
                    Math.Min(Item.Points[Index - 1].X, Item.Points[Index].X) <= Box.CenterX + Box.Width / 2 + Margin
                    && Math.Max(Item.Points[Index - 1].X, Item.Points[Index].X) >= Box.CenterX - Box.Width / 2 - Margin
                    && Math.Min(Item.Points[Index - 1].Y, Item.Points[Index].Y) <= Box.CenterY + Box.Height / 2 + Margin
                    && Math.Max(Item.Points[Index - 1].Y, Item.Points[Index].Y) >= Box.CenterY - Box.Height / 2 - Margin)))
                .Select(Item => Item.Route)];
        }

        internal IEnumerable<string> OldEndpoints(IReadOnlySet<FlowchartLayoutEdgeRoute> Affected) => Routes
            .Where(Item => Affected.Contains(Item.Route)).SelectMany(Item => new[] { Item.Source, Item.Target });

        internal IEnumerable<FlowchartLayoutEdgeRoute> ChangedTerminalRoutes() => Routes.Where(Item => !Item.SameTerminals()).Select(Item => Item.Route);
        internal bool UnchangedOutside(IReadOnlySet<FlowchartLayoutEdgeRoute> Affected) => Routes
            .Where(Item => !Affected.Contains(Item.Route)).All(Item => Item.SameTerminals()
                && Item.Route.Points.Select(Point => (Point.X, Point.Y)).SequenceEqual(Item.Points));

        private sealed class RouteValue
        {
            internal readonly FlowchartLayoutEdgeRoute Route;
            internal readonly string Source, Target;
            internal readonly (double X, double Y)[] Points;
            private readonly string Exit, Entry;
            private readonly (double, double, double, double) Normals;
            internal RouteValue(FlowchartLayoutEdgeRoute Route)
            {
                this.Route = Route; Source = Route.SourceId; Target = Route.TargetId;
                Exit = Route.ExitSide; Entry = Route.EntrySide;
                Normals = (Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY);
                Points = Route.Points.Select(Point => (Point.X, Point.Y)).ToArray();
            }
            internal bool SameTerminals() => Route.SourceId == Source && Route.TargetId == Target
                && Route.ExitSide == Exit && Route.EntrySide == Entry
                && Normals == (Route.ExitNormalX, Route.ExitNormalY, Route.EntryNormalX, Route.EntryNormalY)
                && (Points.Length == 0 ? Route.Points.Count == 0 : Route.Points.Count > 0
                    && Points[0] == (Route.Points[0].X, Route.Points[0].Y) && Points[^1] == (Route.Points[^1].X, Route.Points[^1].Y));
        }
    }
}
