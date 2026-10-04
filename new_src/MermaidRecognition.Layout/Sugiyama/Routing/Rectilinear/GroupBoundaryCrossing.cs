//
// GroupBoundaryCrossing.cs
// MSAGL Obstacle class for a Group boundary crossing for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System.Diagnostics;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear {
    // A Group is a Shape that has children.
    // This class defines a single crossing of a group boundary, from a point on the group boundary.
    // It is intended as the Value of a GroupBoundaryCrossingMap entry, or as an element in a VisiblityEdge.GroupCrossings
    // array, so the actual crossing coordinates are not included.
    internal class GroupBoundaryCrossing {
        // The group to which this applies.
        internal Obstacle Group { get; set; }

        // The direction from the vertex on the group boundary toward the inside of the group.
        internal Directions DirectionToInside { get; private set; }

        internal GroupBoundaryCrossing(Obstacle group, Directions dirToInside) {
            Debug.Assert(CompassVector.IsPureDirection(dirToInside), "Impure direction");
            Group = group;
            DirectionToInside = dirToInside;
        }

        static internal double BoundaryWidth => ApproximateComparer.DistanceEpsilon;

        internal Point GetInteriorVertexPoint(Point outerVertex) => ApproximateComparer.Round(outerVertex + (CompassVector.ToPoint(DirectionToInside) * BoundaryWidth));

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1}", DirectionToInside, Group);
    }
}
