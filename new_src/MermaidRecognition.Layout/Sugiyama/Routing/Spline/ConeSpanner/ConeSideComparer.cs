using System;
using System.Collections.Generic;
using System.Diagnostics;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal class ConeSideComparer : IComparer<ConeSide> {
        Point x;
        internal void SetOperand(ConeSide activeElement) => x = IntersectionOfSegmentAndSweepLine((activeElement));
        readonly IConeSweeper coneSweeper;

        internal ConeSideComparer(IConeSweeper coneSweeper) => this.coneSweeper = coneSweeper;

        public int Compare(ConeSide? a, ConeSide? b) {
            if (a is null)
            {
                return b is null ? 0 : -1;
            }

            if (b is null)
            {
                return 1;
            }

            BrokenConeSide? bObst = b as BrokenConeSide;
            if (a is BrokenConeSide aObst) {
                return bObst != null ? CompareBrokenSides(aObst, bObst) : CompareObstacleSideAndConeSide(b);
            } else {
                //a is ConeSide
                return bObst != null ? CompareConeSideAndObstacleSide(a, bObst) : CompareNotIntersectingSegs(a, b);
            }
        }

        static int CompareNotIntersectingSegs(ConeSide a, ConeSide b) {
            TriangleOrientation signedArea = Point.GetTriangleOrientation(a.Start, b.Start, b.Start + b.Direction);

            switch (signedArea) {
                case TriangleOrientation.Counterclockwise:
                    return -1;
                case TriangleOrientation.Clockwise:
                    return 1;
                default:
                    return 0;
            }
        }

        int CompareObstacleSideAndConeSide(ConeSide coneSide) {
            TriangleOrientation orientation = Point.GetTriangleOrientation(x, coneSide.Start,
                                                                           coneSide.Start + coneSide.Direction);
            if (orientation == TriangleOrientation.Counterclockwise)
            {
                return -1;
            }

            if (orientation == TriangleOrientation.Clockwise)
            {
                return 1;
            }

            //we have the case where x belongs to the cone side

            return coneSide is ConeLeftSide ? -1 : 1;
        }

        int CompareConeSideAndObstacleSide(ConeSide coneSide, BrokenConeSide brokenConeSide) {
            TriangleOrientation orientation = Point.GetTriangleOrientation(x, brokenConeSide.Start, brokenConeSide.End);
            if (orientation == TriangleOrientation.Counterclockwise)
            {
                return -1;
            }

            if (orientation == TriangleOrientation.Clockwise)
            {
                return 1;
            }

            //we have the case where x belongs to the cone side

            //      lineSweeper.Show(CurveFactory.CreateDiamond(5,5, brokenConeSide.EndVertex.Point));

            return coneSide is ConeLeftSide ? 1 : -1;
        }

        internal Point IntersectionOfSegmentAndSweepLine(ConeSide obstacleSide) {
            double den = obstacleSide.Direction*coneSweeper.SweepDirection;
            Debug.Assert(Math.Abs(den) > 0);
            double t = (coneSweeper.Z - obstacleSide.Start*coneSweeper.SweepDirection)/den;
            return obstacleSide.Start + t*obstacleSide.Direction;
        }


        int CompareBrokenSides(BrokenConeSide aObst, BrokenConeSide bObst) {
            if (aObst.EndVertex == bObst.EndVertex)
            {
                return CompareNotIntersectingSegs(aObst.ConeSide, bObst.ConeSide);
            }

            if (Point.GetTriangleOrientation(x, bObst.Start, bObst.EndVertex.Point) ==
                TriangleOrientation.Counterclockwise)
            {
                return -1;
            }

            return 1;
        }
    }
}