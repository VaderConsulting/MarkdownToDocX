using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    /// <summary>
    ///Avoid a situation where two paths cross each other more than once. Remove self loops.
    /// 
    /// </summary>
    internal class PathMerger{
        internal PathMerger(IEnumerable<Path> paths) => Paths = paths;

        internal IEnumerable<Path> Paths { get; set; }

        readonly Dictionary<Point, Dictionary<Path, LinkedPoint>> verticesToPathOffsets =
            [];
            

        /// <summary>
        /// Avoid a situation where two paths cross each other more than once. Remove self loops.
        /// </summary>
        internal void MergePaths(){
            InitVerticesToPathOffsetsAndRemoveSelfCycles();
            foreach (Path path in Paths)
            {
                ProcessPath(path);
            }
        }

        void ProcessPath(Path path) {
            Dictionary<Path, LinkedPoint> departedPaths = new Dictionary<Path, LinkedPoint>();
            Dictionary<Path, LinkedPoint> prevLocationPathOffsets = null;
            for (LinkedPoint linkedPoint = (LinkedPoint) path.PathPoints; linkedPoint != null; linkedPoint = linkedPoint.Next) {
                Dictionary<Path, LinkedPoint> pathOffsets = verticesToPathOffsets[linkedPoint.Point];
                if (prevLocationPathOffsets != null) {
                    //handle returning paths
                    if (departedPaths.Count > 0)
                    {
                        foreach (KeyValuePair<Path, LinkedPoint> pair in pathOffsets) {
                            Path path0 = pair.Key;
                            if (departedPaths.TryGetValue(path0, out LinkedPoint? departerLinkedPoint)) {//returned!
                                CollapseLoopingPath(path0, departerLinkedPoint, pair.Value, path, linkedPoint);
                                departedPaths.Remove(path0);
                            }
                        }
                    }

                    //find departed paths
                    foreach (KeyValuePair<Path, LinkedPoint> pair in prevLocationPathOffsets.Where(pair => !pathOffsets.ContainsKey(pair.Key)))
                    {
                        departedPaths.Add(pair.Key, pair.Value);
                    }
                }
                prevLocationPathOffsets = pathOffsets;
            }
        }

//        bool Correct() {
//            foreach (var kv in verticesToPathOffsets) {
//                Point p = kv.Key;
//                Dictionary<Path, LinkedPoint> pathOffs = kv.Value;
//                foreach (var pathOff in pathOffs) {
//                    var path = pathOff.Key;
//                    var linkedPoint = pathOff.Value;
//                    if (linkedPoint.Point != p)
//                        return false;
//                    if (FindLinkedPointInPath(path, p) == null) {
//                        return false;
//                    }
//                }
//            }
//            return true;
//        }

        void CollapseLoopingPath(Path loopingPath, LinkedPoint departureFromLooping, LinkedPoint arrivalToLooping, Path stemPath, LinkedPoint arrivalToStem){
            LinkedPoint departurePointOnStem = FindLinkedPointInPath(stemPath, departureFromLooping.Point);
            IEnumerable<Point> pointsToInsert = GetPointsInBetween(departurePointOnStem, arrivalToStem);
            if(Before(departureFromLooping, arrivalToLooping)){
                CleanDisappearedPiece(departureFromLooping, arrivalToLooping, loopingPath);
                ReplacePiece(departureFromLooping, arrivalToLooping, pointsToInsert,loopingPath);
            }else{
                CleanDisappearedPiece(arrivalToLooping, departureFromLooping, loopingPath);
                ReplacePiece(arrivalToLooping, departureFromLooping, pointsToInsert.Reverse(),loopingPath);
            }
        }

    
        static IEnumerable<Point> GetPointsInBetween(LinkedPoint a, LinkedPoint b){
            for(LinkedPoint i =a.Next;i!=b;i=i.Next)
            {
                yield return i.Point;
            }
        }

        void ReplacePiece(LinkedPoint a, LinkedPoint b, IEnumerable<Point> points, Path loopingPath){
            LinkedPoint prevPoint = a;
            foreach (Point point in points){
                LinkedPoint lp = new LinkedPoint(point);
                prevPoint.Next = lp;
                prevPoint = lp;
                Dictionary<Path, LinkedPoint> pathOffset = verticesToPathOffsets[point];
                Debug.Assert(!pathOffset.ContainsKey(loopingPath));
                pathOffset[loopingPath] = prevPoint;
            }
            prevPoint.Next = b;
        }

        void CleanDisappearedPiece(LinkedPoint a, LinkedPoint b, Path loopingPath){
            foreach (Point point in GetPointsInBetween(a, b)) {
                Dictionary<Path, LinkedPoint> pathOffset = verticesToPathOffsets[point];
                Debug.Assert(pathOffset.ContainsKey(loopingPath));
                pathOffset.Remove(loopingPath);
            }
        }

        /// <summary>
        /// checks that a is before b in the path
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns>true is a is before b in the path</returns>
        static bool Before(LinkedPoint a, LinkedPoint b){
            for(a=a.Next;a!=null;a=a.Next)
            {
                if (a==b)
                {
                    return true;
                }
            }

            return false;
        }

        static LinkedPoint FindLinkedPointInPath(Path path, Point point) {
            //this function is supposed to always succeed. it will throw a null reference exception otherwise
            for (LinkedPoint linkedPoint = (LinkedPoint) path.PathPoints;; linkedPoint = linkedPoint.Next)
            {
                if (linkedPoint.Point == point)
                {
                    return linkedPoint;
                }
            }
        }

        void InitVerticesToPathOffsetsAndRemoveSelfCycles(){
            foreach(Path path in Paths){
                for (LinkedPoint linkedPoint = (LinkedPoint)path.PathPoints; linkedPoint != null; linkedPoint = linkedPoint.Next) {
                    if (!verticesToPathOffsets.TryGetValue(linkedPoint.Point, out Dictionary<Path, LinkedPoint>? pathOffsets))
                    {
                        verticesToPathOffsets[linkedPoint.Point] = pathOffsets = [];
                    }
                    //check for the loop
                    if (pathOffsets.TryGetValue(path, out LinkedPoint? loopPoint))
                    {//we have a loop
                        CleanDisappearedPiece(loopPoint, linkedPoint, path);
                        loopPoint.Next = linkedPoint.Next;
                    }
                    else
                    {
                        pathOffsets[path] = linkedPoint;
                    }
                }
            }
        }
    }
}