//
// PointAndCrossingsList.cs
// MSAGL class for a sorted list of Group boundary crossings for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Rectilinear {
    // A Group is a Shape that has children.
    // This class maps between intersection points on Group boundaries and the groups and crossing
    // directions at those intersection points.
    internal class PointAndCrossingsList {
        // Internal to allow testing.
        internal List<PointAndCrossings> ListOfPointsAndCrossings { get; private set; }
        int index;

        internal int Count => ListOfPointsAndCrossings.Count;

        internal PointAndCrossingsList() => ListOfPointsAndCrossings = [];

        internal void Add(Point intersect, List<GroupBoundaryCrossing> crossings) => ListOfPointsAndCrossings.Add(new PointAndCrossings(intersect, crossings));

        internal PointAndCrossings Pop() {
            // Next should only be called after CurrentIsBeforeOrAt returns true.
            Debug.Assert(index < ListOfPointsAndCrossings.Count, "Unexpected call to Next()");
            return ListOfPointsAndCrossings[index++];
        }

        internal bool CurrentIsBeforeOrAt(Point comparand) {
            if (index >= ListOfPointsAndCrossings.Count) {
                return false;
            }
            return PointComparer.Compare(ListOfPointsAndCrossings[index].Location, comparand) <= 0;
        }

        internal PointAndCrossings First => ListOfPointsAndCrossings[0];
        internal PointAndCrossings Last => ListOfPointsAndCrossings[ListOfPointsAndCrossings.Count - 1];

        internal void Reset() => index = 0;

        internal void MergeFrom(PointAndCrossingsList other) {
            Reset();
            if ((null == other) || (0 == other.ListOfPointsAndCrossings.Count)) {
                return;
            }
            if (0 == ListOfPointsAndCrossings.Count) {
                ListOfPointsAndCrossings.AddRange(other.ListOfPointsAndCrossings);
            }
            if (null == ListOfPointsAndCrossings) {
                ListOfPointsAndCrossings = new List<PointAndCrossings>(other.ListOfPointsAndCrossings);
                return;
            }

            // Do the usual sorted-list merge.
            int thisIndex = 0, thisMax = ListOfPointsAndCrossings.Count;
            int otherIndex = 0, otherMax = other.ListOfPointsAndCrossings.Count;
            List<PointAndCrossings> newCrossingsList = new List<PointAndCrossings>(ListOfPointsAndCrossings.Count);
            while ((thisIndex < thisMax) || (otherIndex < otherMax)) {
                if (thisIndex >= thisMax) {
                    newCrossingsList.Add(other.ListOfPointsAndCrossings[otherIndex++]);
                    continue;
                }
                if (otherIndex >= otherMax) {
                    newCrossingsList.Add(ListOfPointsAndCrossings[thisIndex++]);
                    continue;
                }

                PointAndCrossings thisPac = ListOfPointsAndCrossings[thisIndex];
                PointAndCrossings otherPac = other.ListOfPointsAndCrossings[otherIndex];
                int cmp = PointComparer.Compare(thisPac.Location, otherPac.Location);
                if (0 == cmp) {
                    // No duplicates
                    newCrossingsList.Add(thisPac);
                    ++thisIndex;
                    ++otherIndex;
                }
                else if (-1 == cmp) {
                    newCrossingsList.Add(thisPac);
                    ++thisIndex;
                }
                else {
                    newCrossingsList.Add(otherPac);
                    ++otherIndex;
                }
            }
            ListOfPointsAndCrossings = newCrossingsList;
        }

        internal void Trim(Point start, Point end) {
            Reset();
            if ((null == ListOfPointsAndCrossings) || (0 == ListOfPointsAndCrossings.Count)) {
                return;
            }

            ListOfPointsAndCrossings = new List<PointAndCrossings>(ListOfPointsAndCrossings.Where(
                    pair => (PointComparer.Compare(pair.Location, start) >= 0) && (PointComparer.Compare(pair.Location, end) <= 0)));
        }

        // For a single vertex point, split its List of crossings in both directions into an array in each (opposite)
        // direction.  CLR Array iteration is much faster than List.
        static internal GroupBoundaryCrossing[] ToCrossingArray(List<GroupBoundaryCrossing> crossings, Directions dirToInside){

            // First find the number in each (opposite) direction, then create the arrays. 
            // We expect a very small number of groups to share a boundary point so this is not optimized.
            int numInDir = 0;
            int crossingsCount = crossings.Count;       // cache for perf
            for (int ii = 0; ii < crossingsCount; ++ii) {
                if (crossings[ii].DirectionToInside == dirToInside) {
                    ++numInDir;
                }
            }
            if (0 == numInDir) {
                return null;
            }

            GroupBoundaryCrossing[] vector = new GroupBoundaryCrossing[numInDir];
            int jj = 0;
            for (int ii = 0; ii < crossingsCount; ++ii) {
                if (crossings[ii].DirectionToInside == dirToInside) {
                    vector[jj++] = crossings[ii];
                }
            }
            return vector;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} [{1}]",
                                ListOfPointsAndCrossings.Count, index);
    }
}
