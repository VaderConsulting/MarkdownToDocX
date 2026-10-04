using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Visibility {
    internal class ObstacleSideComparer : IComparer<SegmentBase> {

        readonly LineSweeperBase lineSweeper;


        internal ObstacleSideComparer(LineSweeperBase lineSweeper) => this.lineSweeper = lineSweeper;

        /// <summary>
        /// the intersection of the sweepline and the active segment
        /// </summary>
        Point x;


        public int Compare(SegmentBase a, SegmentBase b) {
            ValidateArg.IsNotNull(b, "b");
            TriangleOrientation orient = Point.GetTriangleOrientation(b.Start, b.End, x);
            switch (orient) {
                case TriangleOrientation.Collinear:
                    return 0;
                case TriangleOrientation.Clockwise:
                    return 1;
                default:
                    return -1;
            }
        }


        internal void SetOperand(SegmentBase side) => x = IntersectionOfSideAndSweepLine(side);

        internal Point IntersectionOfSideAndSweepLine(SegmentBase obstacleSide) {
            double den = obstacleSide.Direction * lineSweeper.SweepDirection;
            Debug.Assert(Math.Abs(den) > ApproximateComparer.DistanceEpsilon);
            double t = (lineSweeper.Z - obstacleSide.Start * lineSweeper.SweepDirection) / den;
            return obstacleSide.Start + t * obstacleSide.Direction;
        }

    }
}