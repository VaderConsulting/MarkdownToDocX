using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    /// <summary>
    /// If two paths intersect then insert the intersection point as a vertex into both paths.
    /// Remove path self loops. Merge paths between the crossings if they have multiple crossings.
    /// If a path passes through a vertex of another path then insert this vertex into the first path.
    /// </summary>

    internal class PathRefiner {
        
        internal static void RefinePaths(IEnumerable<Path> paths, bool mergePaths) {
            AdjustPaths(paths);
            Dictionary<Path, LinkedPoint> pathsToFirstLinkedVertices = CreatePathsToFirstLinkedVerticesMap(paths);
            Refine(pathsToFirstLinkedVertices.Values);
            CrossVerticalAndHorizontalSegs(pathsToFirstLinkedVertices.Values);
            ReconstructPathsFromLinkedVertices(pathsToFirstLinkedVertices);
            if (mergePaths)
            {
                new PathMerger(paths).MergePaths();
            }
        }
        /// <summary>
        /// make sure that every two different points in paths are separated by at least 10e-6
        /// </summary>
        /// <param name="paths"></param>
        static void AdjustPaths(IEnumerable<Path> paths) {
            foreach (Path path in paths)
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=369
                path.PathPoints = AdjustPathPoints(path.PathPoints.Select(p=>p.Clone()).ToArray()).ToArray();
#else
                path.PathPoints = AdjustPathPoints(path.PathPoints as IList<Point>).ToArray();
#endif
        }

        static IEnumerable<Point> AdjustPathPoints(IList<Point> points) {
            Point p = AdjustPoint(points[0]);
            yield return p;
            for(int i=1;i<points.Count();i++) {
                Point np = AdjustPoint(points[i]);
                if (p!=np)
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=368
                {
                    p = np;
                    yield return p;
                }
#else
                    yield return p = np;
#endif
            }
        }
        static readonly int DigitsToRound = ApproximateComparer.DistanceEpsilonPrecision;

        static Point AdjustPoint(Point p0) => new Point(Math.Round(p0.X, DigitsToRound), Math.Round(p0.Y, DigitsToRound));

        static void CrossVerticalAndHorizontalSegs(IEnumerable<LinkedPoint> pathsFirstLinked) {
            List<LinkedPoint> horizontalPoints = new List<LinkedPoint>();
            List<LinkedPoint> verticalPoints = new List<LinkedPoint>();
            foreach (LinkedPoint pnt in pathsFirstLinked)
            {
                for (LinkedPoint p = pnt; p.Next != null; p = p.Next)
                {
                    if (ApproximateComparer.Close(p.Point.X, p.Next.Point.X))
                    {
                        verticalPoints.Add(p);
                    }
                    else
                    {
                        horizontalPoints.Add(p);
                    }
                }
            }

            (new LinkedPointSplitter(horizontalPoints, verticalPoints)).SplitPoints();
        }


        static void ReconstructPathsFromLinkedVertices(Dictionary<Path, LinkedPoint> pathsToPathLinkedPoints) {
            foreach (KeyValuePair<Path, LinkedPoint> pair in pathsToPathLinkedPoints)
            {
                pair.Key.PathPoints = pair.Value;
            }
        }

        static void Refine(IEnumerable<LinkedPoint> pathFirstPoints) {
            RefineInDirection(Directions.North, pathFirstPoints);
            RefineInDirection(Directions.East, pathFirstPoints);
        }

        /// <summary>
        /// refines all segments that are parallel to "direction"
        /// </summary>
        /// <param name="direction"></param>
        /// <param name="pathFirstPoints"></param>
        static void RefineInDirection(Directions direction, IEnumerable<LinkedPoint> pathFirstPoints) {

            GetProjectionsDelegates(direction, out PointProjection? projectionToPerp, out PointProjection? projectionToDirection);

            LinkedPoint[] linkedPointsInDirection = GetAllLinkedVertsInDirection(projectionToPerp, pathFirstPoints).ToArray();

            IEnumerable<IGrouping<double, LinkedPoint>> colliniarBuckets = linkedPointsInDirection.GroupBy(p => projectionToPerp(p.Point));
            foreach (IGrouping<double, LinkedPoint> pathLinkedPointBucket in colliniarBuckets)
            {
                RefineCollinearBucket(pathLinkedPointBucket, projectionToDirection);
            }
        }

        static void GetProjectionsDelegates(Directions direction,
            out PointProjection projectionToPerp,
            out PointProjection projectionToDirection) {
            if (direction == Directions.East) {
                projectionToDirection = delegate(Point p) { return p.X; };
                projectionToPerp = delegate(Point p) { return p.Y; };
            } else {
                projectionToPerp = delegate(Point p) { return p.X; };
                projectionToDirection = delegate(Point p) { return p.Y; };
            }
        }

        static IEnumerable<LinkedPoint> GetAllLinkedVertsInDirection(
            PointProjection projectionToPerp,
            IEnumerable<LinkedPoint> initialVerts) {
            foreach (LinkedPoint vert in initialVerts)
            {
                for (LinkedPoint v = vert; v.Next != null; v = v.Next)
                {
                    if ( ApproximateComparer.Close(projectionToPerp(v.Point),projectionToPerp(v.Next.Point)))
                    {
                        yield return v;
                    }
                }
            }
        }
        /// <summary>
        /// refine vertices belonging to a bucket; 
        /// pathLinkedVertices belong to a line parallel to the direction of the refinement
        /// </summary>
        /// <param name="pathLinkedVertices"></param>
        /// <param name="projectionToDirection"></param>
        static void RefineCollinearBucket(
            IEnumerable<LinkedPoint> pathLinkedVertices,
            PointProjection projectionToDirection) {

            SortedDictionary<Point, int> dict = new SortedDictionary<Point, int>(new PointByDelegateComparer(projectionToDirection));
            foreach (LinkedPoint pathLinkedPoint in pathLinkedVertices) {
                if (!dict.ContainsKey(pathLinkedPoint.Point))
                {
                    dict[pathLinkedPoint.Point] = 0;
                }

                if (!dict.ContainsKey(pathLinkedPoint.Next.Point))
                {
                    dict[pathLinkedPoint.Next.Point] = 0;
                }
            }

            Point[] arrayOfPoints = new Point[dict.Count];
            int i = 0;
            foreach (Point point in dict.Keys)
            {
                arrayOfPoints[i++] = point;
            }

            for (i = 0; i < arrayOfPoints.Length; i++)
            {
                dict[arrayOfPoints[i]] = i;
            }

            foreach (LinkedPoint pathLinkedVertex in pathLinkedVertices) {
                i = dict[pathLinkedVertex.Point];
                int j = dict[pathLinkedVertex.Next.Point];
                if (Math.Abs(j - i) > 1)
                {
                    InsertPoints(pathLinkedVertex, arrayOfPoints, i, j);
                }
            }
        }

        static void InsertPoints(LinkedPoint pathLinkedVertex, Point[] arrayOfPoints, int i, int j) {
            if (i < j)
            {
                pathLinkedVertex.InsertVerts(i, j, arrayOfPoints);
            }
            else
            {
                pathLinkedVertex.InsertVertsInReverse(j, i, arrayOfPoints);
            }
        }


        static Dictionary<Path, LinkedPoint> CreatePathsToFirstLinkedVerticesMap(IEnumerable<Path> edgePaths) {
            Dictionary<Path, LinkedPoint> dict = new Dictionary<Path, LinkedPoint>();
            foreach (Path path in edgePaths)
            {
                dict[path] = CreateLinkedVertexOfEdgePath(path);
            }

            return dict;
        }

        static LinkedPoint CreateLinkedVertexOfEdgePath(Path path) {
            LinkedPoint pathPoint = new LinkedPoint(path.PathPoints.First());
            LinkedPoint first = pathPoint;
#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=368
            path.PathPoints.Skip(1).Aggregate(pathPoint, (lp, p) => { lp.Next = new LinkedPoint(p); return lp.Next; });
#else
            path.PathPoints.Skip(1).Aggregate(pathPoint, (lp, p) => lp.Next = new LinkedPoint(p));
#endif
            return first;
        }
    }
}