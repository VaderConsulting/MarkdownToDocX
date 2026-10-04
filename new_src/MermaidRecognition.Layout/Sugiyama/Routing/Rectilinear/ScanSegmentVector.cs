using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    internal partial class SparseVisibilityGraphGenerator : VisibilityGraphGenerator {
        /// <summary>
        /// This forms the vector of ScanSegments for the sparse VisibilityGraph.
        /// </summary>
        internal class ScanSegmentVector {
            private readonly ScanSegmentVectorItem[] vector;

            internal ScanSegmentVector(Set<double> coordMap, bool isHorizontal) {
                vector = new ScanSegmentVectorItem[coordMap.Count];
                IsHorizontal = isHorizontal;
                IEnumerator<double> coords = coordMap.OrderBy(coord => coord).GetEnumerator();
                coords.MoveNext();
                for (int ii = 0; ii < vector.Length; ++ii) {
                    vector[ii] = new ScanSegmentVectorItem(coords.Current);
                    coords.MoveNext();
                }
            }

            /// <summary>
            /// The index of the scan segment vector we're appending to on the ScanSegment-generation sweep.
            /// </summary>
            internal int CurrentSlotIndex { get; set; }

            /// <summary>
            /// The number of slots.
            /// </summary>
            internal int Length => vector.Length;

            /// <summary>
            /// The first item in the vector.
            /// </summary>
            internal ScanSegmentVectorItem FirstSlot => vector[0];

            /// <summary>
            /// The last item in the vector
            /// </summary>
            internal ScanSegmentVectorItem LastSlot => vector[vector.Length - 1];

            /// <summary>
            /// The item at the index of the scan segment vector we're appending to on the ScanSegment-generation sweep.
            /// </summary>
            internal ScanSegmentVectorItem CurrentSlot => vector[CurrentSlotIndex];

            /// <summary>
            /// The indexed item in the vector.
            /// </summary>
            internal ScanSegmentVectorItem this[int slot] {
                get { return vector[slot]; }
            }

            /// <summary>
            /// Appends a ScanSegment to the linked list in the "Current" slot.
            /// </summary>
            internal void CreateScanSegment(Point start, Point end, double weight, PointAndCrossingsList gbcList) => CurrentSlot.AppendScanSegment(new ScanSegment(start, end, weight, gbcList));

            internal void ScanSegmentsCompleteForCurrentSlot() => ++CurrentSlotIndex;

            internal void ScanSegmentsComplete() {
                foreach (ScanSegmentVectorItem item in vector) {
                    item.AddPendingPerpendicularCoordsToScanSegments();
                }
            }

            /// <summary>
            /// Returns an enumeration of the vector of ScanSegmentVectorItems.
            /// </summary>
            internal IEnumerable<ScanSegmentVectorItem> Items => vector;

            /// <summary>
            /// Reset vector state between passes.
            /// </summary>
            internal void ResetForIntersections() {
                foreach (ScanSegmentVectorItem t in vector) {
                    t.ResetForIntersections();
                }
            }

            /// <summary>
            /// Indicates if this contains horizontal or vertical ScanSegments.
            /// </summary>
            private bool IsHorizontal { get; set;  }

            /// <summary>
            /// Search the vector for the nearest slot in the specified direction.
            /// </summary>
            internal int FindNearest(double coord, int directionIfMiss) {
                // Array.BinarySearch doesn't allow mapping from ScanSegmentVectorItem to its Coord.
                int low = 0;
                int high = vector.Length - 1;

                if (coord <= vector[low].Coord) {
                    return low;
                }
                if (coord >= vector[high].Coord) {
                    return high;
                }

                while ((high - low) > 2) {
                    int mid = low + ((high - low)/2);
                    ScanSegmentVectorItem item = vector[mid];
                    if (coord < item.Coord) {
                        high = mid;
                        continue;
                    }
                    if (coord > item.Coord) {
                        low = mid;
                        continue;
                    }

                    // TODOsparse - profile - see if I really need the perpCoordMap
                    Debug.Assert(false, "Should not be here if coord is in the vector");
                    return mid;
                }

                // We know the value is between low and high, non-inclusive.
                for (++low; low <= high; ++low) {
                    ScanSegmentVectorItem item = vector[low];
                    if (coord < item.Coord) {
                        return (directionIfMiss > 0) ? low : low - 1;
                    }
                    if (coord == item.Coord) {
                        break;
                    }
                }

                // TODOsparse - profile - see if I really need the perpCoordMap
                Debug.Assert(false, "Should not be here if coord is in the vector");
                return low;
            }

            internal void CreateSparseVerticesAndEdges(VisibilityGraph vg) {
                foreach (ScanSegmentVectorItem item in vector) {
                    item.ResetForIntersections();
                    for (ScanSegment segment = item.FirstSegment; segment != null; segment = segment.NextSegment) {
                        segment.CreateSparseVerticesAndEdges(vg);
                    }
                }
            }

            // Get the coordinate that remains constant along a segment in this vector.
            internal double GetParallelCoord(Point site) => IsHorizontal ? site.Y : site.X;

            // Get the coordinate that changes along a segment in this vector (and is thus the parallel
            // coord of an intersecting segment).
            internal double GetPerpendicularCoord(Point site) => IsHorizontal ? site.X : site.Y;

            internal void ConnectAdjoiningSegmentEndpoints() {
                // Make sure that any series of segments (of different overlappedness) that have points in the
                // graph are connected at adjoining starts/ends and ends/starts (these adjoining points may not be
                // Steiner points in the graph if they are on indirect segments.
                foreach (ScanSegmentVectorItem item in vector) {
                    item.ResetForIntersections();
                    ScanSegment prevSegment = item.FirstSegment;
                    for (ScanSegment segment = prevSegment.NextSegment; segment != null; segment = segment.NextSegment) {
                        if (segment.HasSparsePerpendicularCoords && prevSegment.HasSparsePerpendicularCoords) {
                            if (segment.Start == prevSegment.End) {
                                double perpCoord = GetPerpendicularCoord(segment.Start);
                                prevSegment.AddSparseEndpoint(perpCoord);
                                segment.AddSparseEndpoint(perpCoord);
                            }
                        }
                        prevSegment = segment;
                    }
                }
           }

            /// <summary>
            /// </summary>
            /// <returns></returns>
            public override string ToString() => (IsHorizontal ? "(H) count == " : "(V) count == ") + vector.Count();
        }
    }
}