using System.Collections.Generic;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
      internal class ActiveDiagonalComparerWithRay: IComparer<Diagonal> {

          Point pointOnTheRay;

        internal Point PointOnTangentAndInsertedDiagonal { get => pointOnTheRay; set => pointOnTheRay = value;
          }

        public int Compare(Diagonal? x, Diagonal? y) {
            if (x is null)
            {
                return y is null ? 0 : -1;
            }

            if (y is null)
            {
                return 1;
            }

            ValidateArg.IsNotNull(x, "x");
              ValidateArg.IsNotNull(y, "y");
              System.Diagnostics.Debug.Assert(BelongsToTheDiagonal(PointOnTangentAndInsertedDiagonal, x.Start, x.End));
              if (x.Start != y.Start)
            {
                switch (Point.GetTriangleOrientation(PointOnTangentAndInsertedDiagonal, y.Start, y.End)) {
                      case TriangleOrientation.Counterclockwise:
                          return -1;
                      default:
                          return 1;
                  }
            }
            else {
                  return 0;
              }
          }

        static bool BelongsToTheDiagonal(Point IntersectionOfTheRayAndInsertedEdge, Point start, Point end) => ApproximateComparer.Close(IntersectionOfTheRayAndInsertedEdge, Point.ClosestPointAtLineSegment(IntersectionOfTheRayAndInsertedEdge, start, end));

        internal static Point IntersectDiagonalWithRay(Point pivot, Point pointOnRay, Diagonal diagonal ) {
              Point ray=pointOnRay-pivot;
              Point source=diagonal.Start;
              Point target = diagonal.End;
            //let x(t-s)+s is on the ray, then for some y we x(t-s)+s=y*ray+pivot, or x(t-s)-y*ray=pivot-s
            bool result = LinearSystem2.Solve(target.X - source.X, -ray.X, pivot.X - source.X, target.Y - source.Y, -ray.Y, pivot.Y - source.Y, out double x, out double y);

            System.Diagnostics.Debug.Assert(result && -ApproximateComparer.Tolerance <= x && x <= 1 + ApproximateComparer.Tolerance);
              
              return pivot + (y * ray);
          }

      }
}
