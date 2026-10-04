//
// ScanSegmentVectorItem.cs
// MSAGL base class to create the visibility graph consisting of nlogn ScanSegment intersections for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Rectilinear {
    internal partial class SparseVisibilityGraphGenerator {
        /// <summary>
        /// This forms one slot in the scan segment vector.
        /// </summary>
        internal class ScanSegmentVectorItem {
            /// <summary>
            /// The head of the linked list.
            /// </summary>
            internal ScanSegment FirstSegment;

            /// <summary>
            /// The current segment of the linked list, used when appending or intersecting.
            /// </summary>
            internal ScanSegment CurrentSegment;

            /// <summary>
            /// Perpendicular coordinates that are not in a ScanSegment, due to either not having the ScanSegments created
            /// yet or because it will be faster to do a single pass after accumulating them (e.g. for GroupBoundaryCrossings).
            /// </summary>
            private List<double> pendingPerpCoords;

            internal void AddPendingPerpendicularCoord(double coord) {
                if (pendingPerpCoords == null) {
                    pendingPerpCoords = [];
                }
                pendingPerpCoords.Add(coord);
            }

            /// <summary>
            /// Restores state between intersection passes.
            /// </summary>
            internal void ResetForIntersections() {
                Debug.Assert(null != FirstSegment, "Empty ScanSegmentVectorItem");
                CurrentSegment = FirstSegment;
            }

            /// <summary>
            /// Indicates whether ScanSegments in this item are horizontally or vertically oriented.
            /// </summary>
            internal bool IsHorizontal => !FirstSegment.IsVertical;

            /// <summary>
            /// Returns the constant coordinate of the ScanSegments in this item, i.e. the coordinate
            /// that intersects the perpendicular axis.
            /// </summary>
            internal double Coord { get; private set; }

            /// <summary>
            /// Ctor, taking the parallel (constant) coordinate.
            /// </summary>
            /// <param name="coord">the parallel (constant) coordinate</param>
            internal ScanSegmentVectorItem(double coord) => Coord = coord;

            /// <summary>
            /// Move along the linked list until we hit the ScanSegment that contains the point.
            /// </summary>
            internal bool TraverseToSegmentContainingPoint(Point point) {
                // This is not a simple Next() because scan segments are extended "through" obstacles
                // (intermixing overlapped and non-overlapped) and thus a ScanSegment's Start and End
                // may not be in the vertexPoints collection and the ScanSegment must be skipped.
                if (CurrentSegment.ContainsPoint(point)) {
                    return true;
                }

                double pointCoord = IsHorizontal ? point.Y : point.X;
                if (!PointComparer.Equal(Coord, pointCoord)) {
                    Debug.Assert(PointComparer.Compare(Coord, pointCoord) == -1, "point is before current Coord");
                    while (MoveNext()) {
                        // Skip to the end of the linked list if this point is not on the same coordinate.
                    }
                    return false;
                }

                for (;;) {
                    // In the event of mismatched rounding on horizontal versus vertical intersections
                    // with a sloped obstacle side, we may have a point that is just before or just
                    // after the current segment.  If the point is in some space that doesn't have a
                    // scansegment, and if we are "close enough" to one end or the other of a scansegment,
                    // then grow the scansegment enough to include the new point.
                    if ((null == CurrentSegment.NextSegment) ||
                            PointComparer.GetDirections(CurrentSegment.End, point)
                                == PointComparer.GetDirections(point, CurrentSegment.NextSegment.Start)) {
                        if (ApproximateComparer.CloseIntersections(CurrentSegment.End, point)) {
                            CurrentSegment.Update(CurrentSegment.Start, point);
                            return true;
                        }
                    }

                    if (!MoveNext()) {
                        return false;
                    }
                    if (CurrentSegment.ContainsPoint(point)) {
                        return true;
                    }

                    // This is likely the reverse of the above; the point rounding mismatched to just before
                    // rather than just after the current segment.
                    if (PointComparer.IsPureLower(point, CurrentSegment.Start)) {
                        Debug.Assert(ApproximateComparer.CloseIntersections(CurrentSegment.Start, point), "Skipped over the point in the ScanSegment linked list");
                        CurrentSegment.Update(point, CurrentSegment.End);
                        return true;
                    }
                }
            
            }

            internal bool MoveNext() {
                CurrentSegment = CurrentSegment.NextSegment;
                return HasCurrent;
            }

            internal bool HasCurrent => (null != CurrentSegment);

            /// <summary>
            /// Returns true if the point is the end of the current segment and there is an adjoining NextSegment.
            /// </summary>
            internal bool PointIsCurrentEndAndNextStart(Point point) => (point == CurrentSegment.End) && (null != CurrentSegment.NextSegment) && (point == CurrentSegment.NextSegment.Start);

            /// <summary>
            /// Set Current to the ScanSegment containing the perpendicular coordinate, then add that coordinate to its
            /// sparse-vector coordinate list.
            /// </summary>
            /// <param name="perpCoord"></param>
            internal void AddPerpendicularCoord(double perpCoord) {
                Point point = IsHorizontal ? new Point(perpCoord, Coord) : new Point(Coord, perpCoord);
                TraverseToSegmentContainingPoint(point);
                CurrentSegment.AddSparseVertexCoord(perpCoord);
            }

            /// <summary>
            /// </summary>
            /// <returns></returns>
            public override string ToString() {
                if (null == FirstSegment) {
                    return "-0- " + Coord;
                }
                return (IsHorizontal ? "(H) Y == " : "(V) X == ") + Coord;
            }

            // Create a point from the input coordinate which is on the axis that changes along this item's segments
            // (and is thus the parallel coord of an intersecting segment).
            internal Point CreatePointFromPerpendicularCoord(double endPerpCoord) => IsHorizontal ? new Point(endPerpCoord, Coord) : new Point(Coord, endPerpCoord);

            internal void AppendScanSegment(ScanSegment segment) {
                if (null == FirstSegment) {
                    FirstSegment = segment;
                } else {
                    // Note: segment.Start may != Current.End due to skipping internal ScanSegment creation for non-overlapped obstacles.
                    CurrentSegment.NextSegment = segment;
                }
                CurrentSegment = segment;
            }
            
            internal void AddPendingPerpendicularCoordsToScanSegments() {
                if (pendingPerpCoords != null) {
                    ResetForIntersections();
                    foreach (double point in pendingPerpCoords) {
                        AddPerpendicularCoord(point);
                    }
                }
            }
        }
    }
}