using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry {
    internal class HullPointComparer:IComparer<HullPoint> {
        Point pivot;
      
        /// <summary>
        /// note that this function can change "deleted" member for collinear points
        /// </summary>
        /// <param name="i"></param>
        /// <param name="j"></param>
        /// <returns></returns>
#if SHARPKIT //http://code.google.com/p/sharpkit/issues/detail?id=203
        //SharpKit/Colin - https://code.google.com/p/sharpkit/issues/detail?id=332
        public int Compare(HullPoint? i, HullPoint? j) {
            if (i is null) return j is null ? 0 : -1; if (j is null) return 1;
#else
        int IComparer<HullPoint>.Compare(HullPoint? i, HullPoint? j) {
            if (i is null)
            {
                return j is null ? 0 : -1;
            }

            if (j is null)
            {
                return 1;
            }
#endif
            if (i == j)
            {
                return 0;
            }

            if (i == null)
            {
                return -1;
            }

            if (j == null)
            {
                return 1;
            }

            switch (Point.GetTriangleOrientationWithIntersectionEpsilon(pivot, i.Point, j.Point)) {
                case TriangleOrientation.Counterclockwise:
                    return -1;
                case TriangleOrientation.Clockwise:
                    return 1;
                case TriangleOrientation.Collinear: {
                        //because of the double point error pi and pj can be on different sizes of the pivot on the horizontal line passing through the pivot, or rather just above it
                    double piDelX = i.Point.X - pivot.X;
                    double pjDelX = j.Point.X - pivot.X;
                    if (piDelX > ApproximateComparer.DistanceEpsilon && pjDelX < -ApproximateComparer.DistanceEpsilon) {
                        return -1;
                    }
                    if (piDelX < -ApproximateComparer.DistanceEpsilon && pjDelX > ApproximateComparer.DistanceEpsilon) {
                        return 1;
                    }

                        //here i and j cannot be on the different sides of the pivot because of the choice of the pivot
                        //delete the one that is closer to the pivot.
                        Point pi = i.Point - pivot;
                        Point pj = j.Point - pivot;
                    double iMinJ = pi.L1 - pj.L1;
                    if (iMinJ < 0) {
                        i.Deleted = true;
                        return -1;
                    }
                    if (iMinJ > 0) {
                        j.Deleted = true;
                        return 1;
                    }

                    //points are the same, leave the one with the greatest hash code
                    if (i.GetHashCode() < j.GetHashCode())
                        {
                            i.Deleted = true;
                        }
                        else
                        {
                            j.Deleted = true;
                        }

                        return 0;
                }

            }
            throw new InvalidOperationException();
        }

        internal HullPointComparer(Point pivotPoint) => pivot = pivotPoint;

    }
}
