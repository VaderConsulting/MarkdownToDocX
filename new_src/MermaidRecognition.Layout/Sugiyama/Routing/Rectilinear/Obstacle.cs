//
// Obstacle.cs
// MSAGL Obstacle class for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    // Defines routing information for each obstacle in the graph.
    internal class Obstacle {
        internal const int FirstSentinelOrdinal = 1;
        internal const int FirstNonSentinelOrdinal = 10;

        /// <summary>
        /// Only public to make the compiler happy about the "where TPoly : new" constraint.
        /// Will be populated by caller.
        /// </summary>
        public Obstacle(Shape shape, bool makeRect, double padding) {
            if (makeRect) {
                Rectangle paddedBox = shape.BoundingBox;
                paddedBox.Pad(padding);
                PaddedPolyline = Curve.PolyFromBox(paddedBox);
            } else {
                PaddedPolyline = InteractiveObstacleCalculator.PaddedPolylineBoundaryOfNode(shape.BoundaryCurve, padding);
#if TEST_MSAGL || VERIFY_MSAGL
                // This throws if the polyline is nonconvex.
                VisibilityGraph.CheckThatPolylineIsConvex(PaddedPolyline);
#endif // TEST || VERIFY
            }

            RoundVertices(PaddedPolyline);
            IsRectangle = IsPolylineRectangle();
            if (!IsRectangle) {
                ConvertToRectangleIfClose();
            }
            InputShape = shape;
            Ports = new Set<Port>(InputShape.Ports);
        }


        // From CreateSentinel only
        Obstacle(Point a, Point b, int scanlineOrdinal) {
            PaddedPolyline = new Polyline(ApproximateComparer.Round(a), ApproximateComparer.Round(b)) {Closed = true};
            Ordinal = scanlineOrdinal;
        }

        internal LowObstacleSide ActiveLowSide { get; set; }
        internal HighObstacleSide ActiveHighSide { get; set; }
        internal Shape InputShape { get; set; }
        internal bool IsRectangle { get; private set; }

        /// <summary>
        /// The padded polyline that is tight to the input shape.
        /// </summary>
        internal Polyline PaddedPolyline { get; private set; }

        /// <summary>
        /// The polyline that is either the PaddedPolyline or a convex hull for multiple overlapping obstacles.
        /// </summary>
        internal Polyline VisibilityPolyline => (ConvexHull != null) ? ConvexHull.Polyline : PaddedPolyline;

        /// <summary>
        /// The visibility polyline that is used for intersection comparisons and group obstacle avoidance.
        /// </summary>
        internal Polyline LooseVisibilityPolyline {
            get { 
                if (looseVisibilityPolyline == null) {
                    looseVisibilityPolyline = CreateLoosePolyline(VisibilityPolyline);
                }
                return looseVisibilityPolyline;
            }
        }

        internal static Polyline CreateLoosePolyline(Polyline polyline) {
            Polyline loosePolyline = InteractiveObstacleCalculator.CreatePaddedPolyline(polyline, ApproximateComparer.IntersectionEpsilon * 10);
            RoundVertices(loosePolyline);
            return loosePolyline;
        }

        private Polyline looseVisibilityPolyline;

        internal Rectangle PaddedBoundingBox => PaddedPolyline.BoundingBox;

        internal Rectangle VisibilityBoundingBox => VisibilityPolyline.BoundingBox;

        internal bool IsGroup => (null != InputShape) && InputShape.IsGroup;

        internal bool IsTransparentAncestor
        {
            get => InputShape == null ? false : InputShape.IsTransparent;
            set
            {
                if (InputShape == null)
                {
                    throw new InvalidOperationException();
                }

                InputShape.IsTransparent = value;
            }
        }

        // The ScanLine uses this as a final tiebreaker.  It is set on InitializeEventQueue rather than in
        // AddObstacle to avoid a possible wraparound issue if a lot of obstacles are added/removed.
        // For sentinels, 1/2 are left/right, 3/4 are top/bottom. 0 is invalid during scanline processing.
        internal int Ordinal { get; set; }

        /// <summary>
        /// For overlapping obstacle management; this is just some arbitrary obstacle in the clump.
        /// </summary>
        internal Clump Clump { get; set; }

        internal bool IsOverlapped {
            get {
                Debug.Assert((Clump == null) || !IsGroup, "Groups should not be considered overlapped");
                Debug.Assert((Clump == null) || (ConvexHull == null), "Clumped obstacles should not have overlapped convex hulls");
                return (Clump != null);
            }
        }

        public bool IsInSameClump(Obstacle other) => IsOverlapped && (Clump == other.Clump);

        /// <summary>
        /// For sparseVg, the obstacle has a group corner inside it.
        /// </summary>
        internal bool OverlapsGroupCorner { get; set; }

        // A single convex hull is shared by all obstacles contained by it and we only want one occurrence of that
        // convex hull's polyline in the visibility graph generation.
        internal bool IsPrimaryObstacle => (ConvexHull == null) || (this == ConvexHull.PrimaryObstacle);

        internal OverlapConvexHull ConvexHull { get; private set; }
        internal bool IsInConvexHull => ConvexHull != null;     // Note there is no !IsGroup check

        internal void SetConvexHull(OverlapConvexHull hull) {
            // This obstacle may have been in a rectangular obstacle or clump that was now found to overlap with a non-rectangular obstacle.
            Clump = null;
            IsRectangle = false;
            ConvexHull = hull;
            looseVisibilityPolyline = null;
        }

        // Cloned from InputShape and held to test for Port-membership changes.
        internal Set<Port> Ports { get; private set; }

        internal bool IsSentinel => null == InputShape;

        // Set the initial ActiveLowSide and ActiveHighSide of the obstacle starting at this point.
        internal void CreateInitialSides(PolylinePoint startPoint, ScanDirection scanDir) {
            Debug.Assert((null == ActiveLowSide) && (null == ActiveHighSide)
                         , "Cannot call SetInitialSides when sides are already set");
            ActiveLowSide = new LowObstacleSide(this, startPoint, scanDir);
            ActiveHighSide = new HighObstacleSide(this, startPoint, scanDir);
            if (scanDir.IsFlat(ActiveHighSide)) {
                // No flat sides in the scanline; we'll do lookahead processing in the scanline to handle overlaps
                // with existing segments, and normal neighbor handling will take care of collinear OpenVertexEvents.
                ActiveHighSide = new HighObstacleSide(this, ActiveHighSide.EndVertex, scanDir);
            }
        }

        // Called when we've processed the HighestVertexEvent and closed the object.
        internal void Close() {
            ActiveLowSide = null;
            ActiveHighSide = null;
        }

        internal static Obstacle CreateSentinel(Point a, Point b, ScanDirection scanDir, int scanlineOrdinal) {
            Obstacle sentinel = new Obstacle(a, b, scanlineOrdinal);
            sentinel.CreateInitialSides(sentinel.PaddedPolyline.StartPoint, scanDir);
            return sentinel;
        }

        internal static void RoundVertices(Polyline polyline) {
            // Following creation of the padded border, round off the vertices for consistency
            // in later operations (intersections and event ordering).
            PolylinePoint ppt = polyline.StartPoint;
            do {
                ppt.Point = ApproximateComparer.Round(ppt.Point);
                ppt = ppt.NextOnPolyline;
            } while (ppt != polyline.StartPoint);
            RemoveCloseAndCollinearVerticesInPlace(polyline);

            // We've modified the points so the BoundingBox may have changed; force it to be recalculated.
            polyline.RequireInit();

            // Verify that the polyline is still clockwise.
            Debug.Assert(polyline.IsClockwise(), "Polyline is not clockwise after RoundVertices");
        }

        internal static Polyline RemoveCloseAndCollinearVerticesInPlace(Polyline polyline) {
            double epsilon = ApproximateComparer.IntersectionEpsilon * 10;
            for (PolylinePoint pp = polyline.StartPoint.Next; pp != null; pp = pp.Next) {
                if (ApproximateComparer.Close(pp.Prev.Point, pp.Point, epsilon)) {
                    if (pp.Next == null) {
                        polyline.RemoveEndPoint();
                    } else {
                        pp.Prev.Next = pp.Next;
                        pp.Next.Prev = pp.Prev;
                    }
                }
            }

            if (ApproximateComparer.Close(polyline.Start, polyline.End, epsilon)) {
                polyline.RemoveStartPoint();
            }

            InteractiveEdgeRouter.RemoveCollinearVertices(polyline);
            if ((polyline.EndPoint.Prev != null) && 
                    (Point.GetTriangleOrientation(polyline.EndPoint.Prev.Point, polyline.End, polyline.Start) == TriangleOrientation.Collinear)) {
                polyline.RemoveEndPoint();
            }
            if ((polyline.StartPoint.Next != null) && 
                    (Point.GetTriangleOrientation(polyline.End, polyline.Start, polyline.StartPoint.Next.Point) == TriangleOrientation.Collinear)) {
                polyline.RemoveStartPoint();
            }
            return polyline;
        }

        private bool IsPolylineRectangle () {
            if (PaddedPolyline.PolylinePoints.Count() != 4) {
                return false;
            }

            PolylinePoint ppt = PaddedPolyline.StartPoint;
            PolylinePoint nextPpt = ppt.NextOnPolyline;
            Directions dir = CompassVector.DirectionsFromPointToPoint(ppt.Point, nextPpt.Point);
            if (!CompassVector.IsPureDirection(dir)) {
                return false;
            }
            do {
                ppt = nextPpt;
                nextPpt = ppt.NextOnPolyline;
                Directions nextDir = CompassVector.DirectionsFromPointToPoint(ppt.Point, nextPpt.Point);

                // We know the polyline is clockwise.
                if (nextDir != CompassVector.RotateRight(dir)) {
                    return false;
                }
                dir = nextDir;
            } while (ppt != PaddedPolyline.StartPoint);
            return true;
        }

        // Internal for testing
        internal void ConvertToRectangleIfClose() {
            if (PaddedPolyline.PolylinePoints.Count() != 4) {
                return;
            }

            // We're not a rectangle now but that may be due to rounding error, so we may be close to one.
            // First check that it's close to an axis.
            PolylinePoint ppt = PaddedPolyline.StartPoint;
            PolylinePoint nextPpt = ppt.NextOnPolyline;
            Point testPoint = ppt.Point - nextPpt.Point;
            double slope = ((testPoint.X == 0) || (testPoint.Y == 0)) ? 0 : Math.Abs(testPoint.Y / testPoint.X);
            const double factor = 1000.0;
            if ((slope < factor) && (slope > (1.0/factor))) {
                return;
            }
            const double radian90 = 90.0 * (Math.PI/180.0);
            const double maxAngleDiff = radian90/factor;

            // Now check angles.
            do {
                PolylinePoint nextNextPpt = nextPpt.NextOnPolyline;
                double angle = Point.Angle(ppt.Point, nextPpt.Point, nextNextPpt.Point);
                if (Math.Abs(radian90 - angle) > maxAngleDiff) {
                    return;
                }

                ppt = nextPpt;
                nextPpt = nextNextPpt;
            } while (ppt != PaddedPolyline.StartPoint);

            PaddedPolyline = Curve.PolyFromBox(PaddedPolyline.BoundingBox);
            IsRectangle = true;
            Debug.Assert(IsPolylineRectangle(), "PaddedPolyline is not rectangular");
            return;
        }

        // Return whether there were any port changes, and if so which were added and removed.
        internal bool GetPortChanges(out Set<Port> addedPorts, out Set<Port> removedPorts) {
            addedPorts = InputShape.Ports - Ports;
            removedPorts = Ports - InputShape.Ports;
            if ((0 == addedPorts.Count) && (0 == removedPorts.Count)) {
                return false;
            }
            Ports = new Set<Port>(InputShape.Ports);
            return true;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            string typeString = GetType().ToString();
            int lastDotLoc = typeString.LastIndexOf('.');
            if (lastDotLoc >= 0) {
                typeString = typeString.Substring(lastDotLoc + 1);
            }
            return typeString + " [" + InputShape + "]";
        }
    }
}