//
// StaticGraphUtility.cs
// MSAGL class for static utility functions for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
#if TEST_MSAGL
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
#endif // TEST_MSAGL
using MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    // Static utilities for the visibility graph.
    internal static class StaticGraphUtility {
        // Determine the direction of an edge.
        internal static Directions EdgeDirection(VisibilityEdge edge) => EdgeDirection(edge.Source, edge.Target);
        internal static Directions EdgeDirection(VisibilityVertex source, VisibilityVertex target) => PointComparer.GetPureDirection(source.Point, target.Point);

        internal static VisibilityVertex GetVertex(VisibilityEdge edge, Directions dir) {
            Directions edgeDir = EdgeDirection(edge);
            Debug.Assert(0 != (dir & (edgeDir | CompassVector.OppositeDir(edgeDir))), "dir is orthogonal to edge");
            return (dir == edgeDir) ? edge.Target : edge.Source;
        }
        
        internal static VisibilityVertex FindNextVertex(VisibilityVertex vertex, Directions dir) {
            // This function finds the next vertex in the desired direction relative to the
            // current vertex, not necessarily the edge orientation, hence it does not use
            // EdgeDirection().  This is so the caller can operate on a desired movement
            // direction without having to track whether we're going forward or backward
            // through the In/OutEdge chain.
            int cEdges = vertex.InEdges.Count;      // indexing is faster than foreach for Lists
            for (int ii = 0; ii < cEdges; ++ii) {
                VisibilityEdge edge = vertex.InEdges[ii];
                if (PointComparer.GetPureDirection(vertex.Point, edge.SourcePoint) == dir) {
                    return edge.Source;
                }
            }

            // Avoid GetEnumerator overhead.
            RBNode<VisibilityEdge>? outEdgeNode = vertex.OutEdges.IsEmpty() ? null : vertex.OutEdges.TreeMinimum();
            for (; outEdgeNode != null; outEdgeNode = vertex.OutEdges.Next(outEdgeNode)) {
                VisibilityEdge edge = outEdgeNode.Item;
                if (PointComparer.GetPureDirection(vertex.Point, edge.TargetPoint) == dir) {
                    return edge.Target;
                }
            }
            return null;
        }

        internal static VisibilityEdge FindNextEdge(VisibilityGraph vg, VisibilityVertex vertex, Directions dir) {
            VisibilityVertex nextVertex = FindNextVertex(vertex, dir);
            return (null == nextVertex) ? null : vg.FindEdge(vertex.Point, nextVertex.Point);
        }

        internal static Point FindBendPointBetween(Point sourcePoint, Point targetPoint, Directions finalEdgeDir) {
            Directions targetDir = PointComparer.GetDirections(sourcePoint, targetPoint);
            Debug.Assert(!PointComparer.IsPureDirection(targetDir), "pure direction has no bend");
            Directions firstDir = targetDir & ~finalEdgeDir;
            Debug.Assert(PointComparer.IsPureDirection(firstDir), "firstDir is not pure");

            // Move along the first direction. If the first direction is horizontal then 
            // targetPoint is at the correct horizontal position, and vice-versa.
            return IsVertical(firstDir)
                    ? new Point(sourcePoint.X, targetPoint.Y)
                    : new Point(targetPoint.X, sourcePoint.Y);
        }

        internal static Point SegmentIntersection(Point first, Point second, Point from) {
            Directions dir = PointComparer.GetPureDirection(first, second);
            Point intersect = IsVertical(dir) ? new Point(first.X, from.Y) : new Point(from.X, first.Y);
            return intersect;
        }

        internal static Point SegmentIntersection(SegmentBase seg, Point from) => SegmentIntersection(seg.Start, seg.End, from);

        internal static bool SegmentsIntersect(SegmentBase first, SegmentBase second) => SegmentsIntersect(first, second, out Point intersect);
        internal static bool SegmentsIntersect(SegmentBase first, SegmentBase second, out Point intersect) => IntervalsIntersect(first.Start, first.End, second.Start, second.End, out intersect);
        internal static bool SegmentsIntersect(LineSegment first, LineSegment second, out Point intersect) => IntervalsIntersect(first.Start, first.End, second.Start, second.End, out intersect);
        internal static Point SegmentIntersection(SegmentBase first, SegmentBase second) {
            // Caller expects segments to intersect.
            if (!SegmentsIntersect(first, second, out Point intersect))
            {
                Debug.Assert(false, "intersect is not on both segments");
            }
            return intersect;
        }

        internal static bool IntervalsOverlap(SegmentBase first, SegmentBase second) => IntervalsOverlap(first.Start, first.End, second.Start, second.End);
        internal static bool IntervalsOverlap(LineSegment first, LineSegment second) => IntervalsOverlap(first.Start, first.End, second.Start, second.End);
        internal static bool IntervalsOverlap(Point start1, Point end1, Point start2, Point end2) => IntervalsAreCollinear(start1, end1, start2, end2)
                && PointComparer.Compare(start1, end2) != PointComparer.Compare(end1, start2);
        internal static bool IntervalsAreCollinear(SegmentBase first, SegmentBase second) => IntervalsAreCollinear(first.Start, first.End, second.Start, second.End);
        internal static bool IntervalsAreCollinear(Point start1, Point end1, Point start2, Point end2) {
            Debug.Assert(IsVertical(start1, end1) == IsVertical(start2, end2), "segments are not in the same orientation");
            bool vertical = IsVertical(start1, end1);
            if (IsVertical(start2, end2) == vertical) {
                // This handles touching endpoints as well.
                return vertical ? PointComparer.Equal(start1.X, start2.X) : PointComparer.Equal(start1.Y, start2.Y);
            }
            return false;
        }
        internal static bool IntervalsAreSame(Point start1, Point end1, Point start2, Point end2) => PointComparer.Equal(start1, start2) && PointComparer.Equal(end1, end2);
        internal static bool IntervalsIntersect(Point firstStart, Point firstEnd, Point secondStart, Point secondEnd, out Point intersect) {
            Debug.Assert(IsVertical(firstStart, firstEnd) != IsVertical(secondStart, secondEnd), "cannot intersect two parallel segments");
            intersect = SegmentIntersection(firstStart, firstEnd, secondStart);
            return PointIsOnSegment(firstStart, firstEnd, intersect)
                && PointIsOnSegment(secondStart, secondEnd, intersect);
        }

        internal static Point SegmentIntersection(VisibilityEdge edge, Point from) => SegmentIntersection(edge.SourcePoint, edge.TargetPoint, from);

        internal static VisibilityVertex LowVertex(VisibilityEdge edge) => IsAscending(edge) ? edge.Source : edge.Target;

        internal static VisibilityVertex HighVertex(VisibilityEdge edge) => IsAscending(edge) ? edge.Target : edge.Source;

        internal static bool PointIsOnSegment(Point first, Point second, Point test) => PointComparer.Equal(first, test)
                || PointComparer.Equal(second, test)
                || (PointComparer.GetPureDirection(first, test) == PointComparer.GetPureDirection(test, second));
        internal static bool PointIsOnSegment(SegmentBase seg, Point test) => PointIsOnSegment(seg.Start, seg.End, test);
        internal static bool PointIsOnSegment(LineSegment seg, Point test) => PointIsOnSegment(seg.Start, seg.End, test);

        internal static bool PointIsOnSegmentInterior(Point first, Point second, Point test) {
            Directions firstDir = PointComparer.GetDirections(first, test);
            Directions secondDir = PointComparer.GetDirections(test, second);
            Debug.Assert((Directions. None != firstDir) || (Directions. None != secondDir), "zero-length segment");
            return (firstDir == secondDir);
        }
        internal static bool PointIsOnSegmentInterior(LineSegment seg, Point test) => PointIsOnSegmentInterior(seg.Start, seg.End, test);
        internal static bool PointIsOnSegmentInterior(SegmentBase seg, Point test) => PointIsOnSegmentInterior(seg.Start, seg.End, test);

        internal static bool IsVertical(Directions dir) => (0 != (dir & (Directions.North | Directions.South)));
        internal static bool IsVertical(VisibilityEdge edge) => IsVertical(PointComparer.GetPureDirection(edge.SourcePoint, edge.TargetPoint));
        internal static bool IsVertical(Point first, Point second) => IsVertical(PointComparer.GetPureDirection(first, second));
        internal static bool IsVertical(SegmentBase seg) => IsVertical(PointComparer.GetPureDirection(seg.Start, seg.End));
        internal static bool IsVertical(LineSegment seg) => IsVertical(PointComparer.GetPureDirection(seg.Start, seg.End));

        internal static bool IsAscending(Directions dir) => (0 != (dir & (Directions.North | Directions.East)));
        internal static bool IsAscending(VisibilityEdge edge) => IsAscending(EdgeDirection(edge));

        internal static double Slope(SegmentBase seg, ScanDirection scanDir) => Slope(seg.Start, seg.End, scanDir);

        internal static double Slope(Point start, Point end, ScanDirection scanDir) {
            // Find the slope relative to scanline - how much scan coord changes per sweep change.
            Point lineDirAsPoint = end - start;
            return (lineDirAsPoint * scanDir.PerpDirectionAsPoint) / (lineDirAsPoint * scanDir.DirectionAsPoint);
        }

        internal static Tuple<Point, Point> SortAscending(Point a, Point b) {
            Directions dir = PointComparer.GetDirections(a, b);
            Debug.Assert((Directions. None == dir) || PointComparer.IsPureDirection(dir), "SortAscending with impure direction");
            return ((Directions. None == dir) || IsAscending(dir)) ? new Tuple<Point, Point>(a, b) : new Tuple<Point, Point>(b, a);
        }

        internal static Point RectangleBorderIntersect(Rectangle boundingBox, Point point, Directions dir) {
            switch (dir) {
                case Directions.North:
                case Directions.South:
                    return new Point(point.X, GetRectangleBound(boundingBox, dir));
                case Directions.East:
                case Directions.West:
                    return new Point(GetRectangleBound(boundingBox, dir), point.Y);
                default:
                    throw new InvalidOperationException(
#if TEST_MSAGL
                            "Invalid direction"
#endif // TEST
                        );
            }
        }

        internal static double GetRectangleBound(Rectangle rect, Directions dir) {
            switch (dir) {
                case Directions.North:
                    return rect.Top;
                case Directions.South:
                    return rect.Bottom;
                case Directions.East:
                    return rect.Right;
                case Directions.West:
                    return rect.Left;
                default:
                    throw new InvalidOperationException(
#if TEST_MSAGL
                            "Invalid direction"
#endif // TEST
                        );
            }
        }

        internal static bool RectangleInteriorsIntersect(Rectangle a, Rectangle b) => (PointComparer.Compare(a.Bottom, b.Top) < 0)
                && (PointComparer.Compare(b.Bottom, a.Top) < 0)
                && (PointComparer.Compare(a.Left, b.Right) < 0)
                && (PointComparer.Compare(b.Left, a.Right) < 0);

        internal static bool PointIsInRectangleInterior(Point point, Rectangle rect) => (PointComparer.Compare(point.Y, rect.Top) < 0)
                && (PointComparer.Compare(rect.Bottom, point.Y) < 0)
                && (PointComparer.Compare(point.X, rect.Right) < 0)
                && (PointComparer.Compare(rect.Left, point.X) < 0);

        [Conditional("DEBUG")]
        internal static void Assert(bool condition, string message, ObstacleTree obstacleTree, VisibilityGraph vg) {
            if (!condition) {
                Test_DumpVisibilityGraph(obstacleTree, vg);
                Debug.Assert(condition, message);
            }
        }

        [Conditional("TEST_MSAGL")]
// ReSharper disable InconsistentNaming
        internal static void Test_ShowVisibilityGraph(ObstacleTree obstacleTree, VisibilityGraph vg) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetVisibilityGraphDebugCurves(vg));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debugCurves);
#endif // TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_DumpVisibilityGraph(ObstacleTree obstacleTree, VisibilityGraph vg) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetVisibilityGraphDebugCurves(vg));
            DebugCurveCollection.WriteToFile(debugCurves, GetDumpFileName("VisibilityGraph"));
#endif // TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_ShowPathsBeforeNudging(ObstacleTree obstacleTree, IEnumerable<Path> edgePaths) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetPreNudgedPathDebugCurves(edgePaths));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debugCurves);
#endif //TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_DumpPathsBeforeNudging(ObstacleTree obstacleTree, IEnumerable<Path> edgePaths) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetPreNudgedPathDebugCurves(edgePaths));
            DebugCurveCollection.WriteToFile(debugCurves, GetDumpFileName("PreNudgedPaths"));
#endif //TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_ShowPathsAfterNudging(ObstacleTree obstacleTree, IEnumerable<Path> edgePaths) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetPostNudgedPathDebugCurves(edgePaths));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debugCurves);
#endif // TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_DumpPathsAfterNudging(ObstacleTree obstacleTree, IEnumerable<Path> edgePaths) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetPostNudgedPathDebugCurves(edgePaths));
            DebugCurveCollection.WriteToFile(debugCurves, GetDumpFileName("PostNudgedPaths"));
#endif // TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_ShowScanSegments(ObstacleTree obstacleTree, ScanSegmentTree hSegs, ScanSegmentTree vSegs) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetScanSegmentCurves(hSegs));
            debugCurves.AddRange(Test_GetScanSegmentCurves(vSegs));
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(debugCurves);
#endif // TEST
        }

        [Conditional("TEST_MSAGL")]
        internal static void Test_DumpScanSegments(ObstacleTree obstacleTree, ScanSegmentTree hSegs, ScanSegmentTree vSegs) {
#if TEST_MSAGL
            List<DebugCurve> debugCurves = Test_GetObstacleDebugCurves(obstacleTree);
            debugCurves.AddRange(Test_GetScanSegmentCurves(hSegs));
            debugCurves.AddRange(Test_GetScanSegmentCurves(vSegs));
            DebugCurveCollection.WriteToFile(debugCurves, GetDumpFileName("ScanSegments"));
#endif // TEST
        }

#if TEST_MSAGL
        internal static List<DebugCurve> Test_GetObstacleDebugCurves(ObstacleTree obstacleTree) => Test_GetObstacleDebugCurves(obstacleTree, false, false);

        internal static List<DebugCurve> Test_GetObstacleDebugCurves(ObstacleTree obstacleTree, bool noPadPoly, bool noVisPoly) => Test_GetObstacleDebugCurves(obstacleTree.GetAllObstacles(), noPadPoly, noVisPoly);

        internal static List<DebugCurve> Test_GetObstacleDebugCurves(IEnumerable<Obstacle> obstacles, bool noPadPoly, bool noVisPoly) {
            List<DebugCurve> debugCurves = new List<DebugCurve>();
            foreach (Obstacle obstacle in obstacles) {
                debugCurves.Add(new DebugCurve(0.1, "darkgray", obstacle.InputShape.BoundaryCurve));
                if (!noPadPoly || obstacle.IsGroup) {
                    debugCurves.Add(obstacle.IsTransparentAncestor
                            ? new DebugCurve(0.3, "gold", obstacle.PaddedPolyline)
                            : new DebugCurve(0.1, obstacle.IsGroup ? "purple" : "black", obstacle.PaddedPolyline));
                }
                if (!noVisPoly && obstacle.IsPrimaryObstacle && (obstacle.VisibilityPolyline != obstacle.PaddedPolyline)) {
                    debugCurves.Add(new DebugCurve(0.1, obstacle.IsGroup ? "mediumpurple" : "lightgray", obstacle.VisibilityPolyline));
                }
            }
            return debugCurves;
        }

        internal static List<DebugCurve> Test_GetVisibilityGraphDebugCurves(VisibilityGraph vg) => vg.Edges.Select(edge => new DebugCurve(0.1,
                        (edge.Weight == ScanSegment.NormalWeight) ? "Blue"
                                : ((edge.Weight == ScanSegment.ReflectionWeight) ? "DarkCyan" : "LightBlue"),
                        new LineSegment(edge.Source.Point, edge.Target.Point))).ToList();

        internal static List<DebugCurve> Test_GetPreNudgedPathDebugCurves(IEnumerable<Path> edgePaths) {
            List<DebugCurve> debugCurves = new List<DebugCurve>();
            foreach (Path path in edgePaths) {
                Point[] points = path.PathPoints.ToArray();
                for (int ii = 0; ii < points.Length - 1; ++ii) {
                    debugCurves.Add(new DebugCurve(0.1, "purple", new LineSegment(points[ii], points[ii + 1])));
                }
            }
            return debugCurves;
        }

        internal static List<DebugCurve> Test_GetPostNudgedPathDebugCurves(IEnumerable<Path> edgePaths) {
            List<DebugCurve> debugCurves = new List<DebugCurve>();
            foreach (Path path in edgePaths) {
                debugCurves.AddRange(path.PathEdges.Select
                        (e => new DebugCurve(0.1, "purple", new LineSegment(e.Source, e.Target))));
            }
            return debugCurves;
        }

        internal static List<DebugCurve> Test_GetScanSegmentCurves(ScanSegmentTree segTree) => segTree.Segments.Select(seg => new DebugCurve(0.2,
                        seg.IsOverlapped ? "Aqua" : (seg.IsReflection ? "LightGreen" : "DarkGreen"),
                        new LineSegment(seg.Start, seg.End))).ToList();

        internal static string GetDumpFileName(string prefix) => System.IO.Path.GetTempPath() + prefix + ".DebugCurves";
#endif // TEST
        // ReSharper restore InconsistentNaming

#if DEBUG
        // Make it easier for floating-point conditional breakpoints in the VS debugger
        // (the docs say they don't need to be non-private but apparently they do).
        internal static bool IsEqualForDebugger(double variable, double want) => (variable < (want + 1.0)) && (variable > (want - 1.0));
        internal static bool IsEqualForDebugger(Point variable, double wantX, double wantY) => IsEqualForDebugger(variable.X, wantX) && IsEqualForDebugger(variable.Y, wantY);
#endif // DEBUG
    }
}