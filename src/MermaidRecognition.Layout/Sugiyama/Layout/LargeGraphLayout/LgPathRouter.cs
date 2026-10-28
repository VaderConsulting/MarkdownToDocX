using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Routing;
using MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;
using SymmetricSegment = MermaidRecognition.Layout.Sugiyama.Core.DataStructures.SymmetricTuple<MermaidRecognition.Layout.Sugiyama.Core.Geometry.Point>;
namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    internal class LgPathRouter {
        VisibilityGraph _visGraph; // = new VisibilityGraph();

        internal const double searchEps = 1e-5;
        readonly RTree<VisibilityVertex> _visGraphVerticesTree = new RTree<VisibilityVertex>();

        readonly Dictionary<VisibilityEdge,int> _usedEdges = [];

        readonly Set<VisibilityEdge> _edgesOnOldTrajectories = []; 

        internal VisibilityGraph VisGraph { get => _visGraph; set => _visGraph = value;
        }

        internal LgPathRouter() => _visGraph = new VisibilityGraph();

        internal bool IsOnOldTrajectory(VisibilityEdge e) => _edgesOnOldTrajectories.Contains(e);

        internal void MarkEdgeUsed(Point p0, Point p1) {
            VisibilityEdge e = FindEdge(p0, p1);
            if (!_usedEdges.TryGetValue(e, out int usage))
            {
                _usedEdges[e] = 1;
            }
            else
            {
                _usedEdges[e]++;
            }
        }

        internal void MarkEdgeAsEdgeOnOldTrajectory(Point p0, Point p1) {
            VisibilityEdge e = FindEdge(p0, p1);
            _edgesOnOldTrajectories.Insert(e);
        }

        internal bool IsEdgeUsed(VisibilityEdge e) {
            if (!_usedEdges.TryGetValue(e, out int usage))
            {
                return false;
            }

            return usage > 0;
        }

        internal void MarkEdgesUsedAlongPath(List<Point> path) {
            for (int i = 0; i < path.Count - 1; i++)
            {
                MarkEdgeUsed(path[i], path[i + 1]);
            }
        }

        internal void MarkEdgesAlongPathAsEdgesOnOldTrajectories(List<Point> path) {
            for (int i = 0; i < path.Count - 1; i++)
            {
                MarkEdgeAsEdgeOnOldTrajectory(path[i], path[i + 1]);
            }
        }

        internal VisibilityEdge AddVisGraphEdge(Point ep0, Point ep1) {
            VisibilityVertex v0 = GetOrFindVisibilityVertex(ep0) ?? AddNewVertex(ep0);
            VisibilityVertex v1 = GetOrFindVisibilityVertex(ep1) ?? AddNewVertex(ep1);
            return VisibilityGraph.AddEdge(v0, v1);
        }


        internal void AddVisGraphEdgesFromNodeCenterToNodeBorder(LgNodeInfo nodeInfo) {
            VisibilityVertex vc = VisGraph.AddVertex(nodeInfo.Center);
            vc.IsTerminal = true; // we don't need to register this node in the tree
            foreach (PolylinePoint pt in nodeInfo.BoundaryOnLayer.PolylinePoints) {
                VisibilityVertex vv = GetOrFindVisibilityVertex(pt.Point);
                VisibilityEdge edge = VisibilityGraph.AddEdge(vc, vv);
                edge.IsPassable = () => EdgeIsPassable(edge);
            }
        }

        internal void RemoveVisGraphVertex(Point p) {
            VisibilityVertex v = _visGraph.FindVertex(p);
            if (v == null)
            {
                return;
            }

            _visGraph.RemoveVertex(v);
            _visGraphVerticesTree.Remove(new Rectangle(p), v);
        }

        internal VisibilityVertex GetOrFindVisibilityVertex(Point p) {
            VisibilityVertex v = _visGraph.FindVertex(p);
            if (v != null)
            {
                return v;
            }

            v =_visGraph.AddVertex(p);
            RegisterInTree(v);
            return v;
        }

        internal void ModifySkeletonWithNewBoundaryOnLayer(LgNodeInfo nodeInfo) {
            Dictionary<VisibilityEdge, VisibilityVertex> edgeSnapMap = GetEdgeSnapAtVertexMap(nodeInfo);            
            foreach (KeyValuePair<VisibilityEdge, VisibilityVertex> t in edgeSnapMap) {
                VisibilityEdge edge = t.Key;
                VisibilityGraph.RemoveEdge(edge);
                VisibilityVertex midleV = edgeSnapMap[edge];
                if (nodeInfo.Center != edge.SourcePoint)
                {
                    VisibilityGraph.AddEdge(edge.Source, midleV);
                }
                else
                {
                    VisibilityGraph.AddEdge(midleV, edge.Target);
                }
            }
        }

        static void SortPointByAngles(LgNodeInfo nodeInfo, Point[] polySplitArray) {
            double[] angles = new double[polySplitArray.Length];
            for (int i = 0; i < polySplitArray.Length; i++)
            {
                angles[i] = Point.Angle(new Point(1, 0), polySplitArray[i] - nodeInfo.Center);
            }

            Array.Sort(angles, polySplitArray);
        }

        VisibilityVertex GlueOrAddToPolylineAndVisGraph(Point[] polySplitArray, int i, VisibilityVertex v, Polyline poly) {
            Point ip = polySplitArray[i];
            if (ApproximateComparer.Close(v.Point, ip))
            {
                return v; // gluing ip to the previous point on the polyline
            }

            if (ApproximateComparer.Close(ip, poly.StartPoint.Point)) {
                VisibilityVertex vv = VisGraph.FindVertex(poly.StartPoint.Point);
                Debug.Assert(vv != null);
                return vv;
            }
            poly.AddPoint(ip);
            return VisGraph.AddVertex(ip);
        }

        Dictionary<VisibilityEdge, VisibilityVertex> GetEdgeSnapAtVertexMap(LgNodeInfo nodeInfo) {
            Dictionary<VisibilityEdge, VisibilityVertex> ret = [];
            Point center = nodeInfo.Center;
            RbTree<VisibilityVertex> nodeBoundaryRbTree =
                new RbTree<VisibilityVertex>((a, b) => CompareByAngleFromNodeCenter(a, b, center));
            foreach (Point p in nodeInfo.BoundaryOnLayer)
            {
                nodeBoundaryRbTree.Insert(_visGraph.AddVertex(p));
            }

            VisibilityVertex nodeInfoCenterV = VisGraph.FindVertex(center);
            if (nodeInfoCenterV == null)
            {
                return ret;
            }

            foreach (VisibilityEdge e in nodeInfoCenterV.OutEdges)
            {
                SnapToAfterBefore(e.Target, nodeBoundaryRbTree, center, ret, e);
            }

            foreach (VisibilityEdge e in nodeInfoCenterV.InEdges)
            {
                SnapToAfterBefore(e.Source, nodeBoundaryRbTree, center, ret, e);
            }

            return ret;
        }

        void SnapToAfterBefore(VisibilityVertex v, RbTree<VisibilityVertex> nodeBoundaryRbTree, Point center, Dictionary<VisibilityEdge, VisibilityVertex> ret, VisibilityEdge e) {
            FindBeforeAfterV(v, nodeBoundaryRbTree, out VisibilityVertex? beforeV, out VisibilityVertex? afterV, center);
            double beforeAngle = Point.Angle(beforeV.Point - center, v.Point - center);
            double afterAngle = Point.Angle(v.Point - center, afterV.Point - center);
            ret[e] = beforeAngle <= afterAngle ? beforeV : afterV;
        }

        void FindBeforeAfterV(VisibilityVertex v, RbTree<VisibilityVertex> nodeBoundaryRbTree,
            out VisibilityVertex beforeV, out VisibilityVertex afterV, Point center) {
            Point xDir=new Point(1,0);
            double vAngle = Point.Angle(xDir, v.Point - center);
            RBNode<VisibilityVertex> rNode = nodeBoundaryRbTree.FindLast(w => Point.Angle(xDir, w.Point - center) <= vAngle);
            beforeV = rNode!=null? rNode.Item : nodeBoundaryRbTree.TreeMaximum().Item;
            rNode = nodeBoundaryRbTree.FindFirst(w => Point.Angle(xDir, w.Point - center) >= vAngle);
            afterV = rNode != null ? rNode.Item : nodeBoundaryRbTree.TreeMinimum().Item;
        }

        int CompareByAngleFromNodeCenter(VisibilityVertex a, VisibilityVertex b, Point center) {
            Point x = new Point(1, 0);
            double aAngle = Point.Angle(x, a.Point - center);
            double bAngle = Point.Angle(x, b.Point - center);
            return aAngle.CompareTo(bAngle);
        }

        VisibilityVertex AddNewVertex(Point p) {
            VisibilityVertex v = _visGraph.AddVertex(p);
            RegisterInTree(v);
            return v;
        }

        internal Point AddVisGraphVertex(Point p) {
            if (_visGraph.ContainsVertex(p))
            {
                return p;
            }

            AddNewVertex(p);
            return p;
        }

        internal Point[] GetPortVertices(LgNodeInfo node) => [.. node.BoundaryOnLayer.PolylinePoints.Select(pp => pp.Point)];

        internal List<Point> GetPath(VisibilityVertex vs, VisibilityVertex vt,
            bool shrinkDistances) {
            List<Point> pathPoints = [];

            vs.IsShortestPathTerminal = vt.IsShortestPathTerminal = true;
            _visGraph.ClearPrevEdgesTable();
            SingleSourceSingleTargetShortestPathOnVisibilityGraph router = new SingleSourceSingleTargetShortestPathOnVisibilityGraph(_visGraph, vs, vt)
            {
                LengthMultiplier = 0.8
            };
            IEnumerable<VisibilityVertex> vpath = router.GetPath(shrinkDistances);
            if (vpath == null) {
                Console.WriteLine("seeing a null path");
                vs.IsShortestPathTerminal = vt.IsShortestPathTerminal = false; 
                return pathPoints;
            }
            List<VisibilityVertex> path = [.. vpath];
            for (int i = 0; i < path.Count(); i++)
            {
                pathPoints.Add(path[i].Point);
            }

            vs.IsShortestPathTerminal = vt.IsShortestPathTerminal = false;                 
            return pathPoints;
        }

        
        internal List<VisibilityEdge> GetEdgesOfPath(List<Point> pathPoints) {
            List<VisibilityEdge> edges = [];
            for (int i = 0; i < pathPoints.Count - 1; i++) {
                VisibilityVertex v0 = GetOrFindVisibilityVertex(pathPoints[i]);
                if (v0 == null)
                {
                    continue;
                }

                VisibilityVertex v1 = GetOrFindVisibilityVertex(pathPoints[i + 1]);
                if (v1 == null)
                {
                    continue;
                }

                VisibilityEdge edge = _visGraph.FindEdge(v0.Point, v1.Point);
                if (edge != null)
                {
                    edges.Add(edge);
                }
            }
            return edges;
        }

        internal VisibilityEdge FindEdge(Point p1, Point p2) {
            VisibilityVertex v0 = FindVertex(p1);
            if (v0 == null)
            {
                return null;
            }

            VisibilityVertex v1 = FindVertex(p2);
            return v1 == null ? null : _visGraph.FindEdge(v0.Point, v1.Point);
        }

        internal List<Point> GetPath(LgNodeInfo s, LgNodeInfo t, bool shrinkDistances) {
            VisibilityVertex vs = VisGraph.FindVertex(s.Center);
            Debug.Assert(vs!=null);
            VisibilityVertex vt = VisGraph.FindVertex(t.Center);
            Debug.Assert(vt!=null);
            return GetPath(vs, vt, shrinkDistances);
        }

        internal List<VisibilityVertex> GetAllVertices() => [.. (_visGraph.Vertices())];

        internal List<Point> GetPointsOfVerticesOverlappingSegment(Point p0, Point p1) {
            VisibilityVertex v0 = VisGraph.FindVertex(p0);
            VisibilityVertex v1 = VisGraph.FindVertex(p1);
            return GetPath(v0, v1, true);
        }


        internal IEnumerable<VisibilityEdge> GetAllEdgesVisibilityEdges() => _visGraph.Edges;


        internal bool ExistsEdge(Point s, Point t) {
            VisibilityVertex p0 = GetOrFindVisibilityVertex(s);
            if (p0 == null)
            {
                return false;
            }

            VisibilityVertex p1 = GetOrFindVisibilityVertex(t);
            if (p1 == null)
            {
                return false;
            }

            return _visGraph.FindEdge(p0.Point, p1.Point) != null;
        }

        internal List<Point> GetPathOnSavedTrajectory(LgNodeInfo s, LgNodeInfo t, List<Point> trajectory,
            bool shrinkDistances) {
            if (trajectory == null || trajectory.Count < 2)
            {
                return GetPath(s, t, true);
            }

            List<Point> path = [trajectory[0]];
            for (int i = 0; i < trajectory.Count - 1; i++) {
                Point p0 = trajectory[i];
                Point p1 = trajectory[i + 1];
                List<Point> refinedPath = GetPointsOfVerticesOverlappingSegment(p0, p1);
                for (int j = 1; j < refinedPath.Count; j++)
                {
                    path.Add(refinedPath[j]);
                }
            }
            
            return path;
        }

        internal bool HasCycles(Point rootPoint) {
            Set<Point> visited = [];
            Dictionary<Point, Point> parent = [];

            Point rp = AddVisGraphVertex(rootPoint);
            visited.Insert(rp);
            parent[rp] = rp;

            List<Point> queue = [rp];

            while (queue.Any()) {
                Point p = queue.First();
                queue.Remove(p);

                VisibilityVertex v = _visGraph.FindVertex(p);
                Set<Point> neighb = [];
                neighb.InsertRange(v.OutEdges.Select(e => e.TargetPoint));
                neighb.InsertRange(v.InEdges.Select(e => e.SourcePoint));
                neighb.Remove(parent[p]);

                foreach (Point q in neighb) {
                    parent[q] = p;
                    if (visited.Contains(q))
                    {
                        return true;
                    }

                    visited.Insert(q);
                    queue.Add(q);
                }
            }

            return false;
        }

        internal void DecreaseWeightOfEdgesAlongPath(List<Point> oldPath, double d) {
            List<VisibilityEdge> edges = GetEdgesOfPath(oldPath);
            foreach (VisibilityEdge edge in edges) {
                edge.LengthMultiplier = Math.Min(edge.LengthMultiplier, d);
            }
        }

        internal void ResetAllEdgeLengthMultipliers() {
            IEnumerable<VisibilityEdge> edges = _visGraph.Edges;
            foreach (VisibilityEdge edge in edges) {
                edge.LengthMultiplier = 1;
            }
        }

        internal bool ContainsVertex(Point point) => VisGraph.FindVertex(point) != null;

        internal VisibilityVertex FindVertex(Point point) => VisGraph.FindVertex(point);

        internal void RemoveEdge(Point a, Point b) {
            VisibilityEdge e = VisGraph.FindEdge(a, b);
            _usedEdges.Remove(e);

            _edgesOnOldTrajectories.Remove(e);

            _visGraph.RemoveEdge(e.Source, e.Target);
        }

        internal void RemoveVisibilityEdges(List<VisibilityEdge> edgesToRemove) {
            foreach (VisibilityEdge e in edgesToRemove) {
                _visGraph.RemoveEdge(e.Source, e.Target);
                if (e.Source.Degree == 0)
                {
                    RemoveVisGraphVertex(e.SourcePoint);
                }

                if (e.Target.Degree == 0)
                {
                    RemoveVisGraphVertex(e.TargetPoint);
                }
            }
        }

        internal void AddEdges(List<SymmetricSegment> toAdd) {
            foreach (SymmetricSegment e in toAdd) {
                AddVisGraphEdge(e.A, e.B);
            }
        }

//        internal VisibilityVertex GetExistingVvCloseBy(Point p) {
//            var rect = new Rectangle(p);
//            rect.Pad(searchEps);
//            VisibilityVertex v=null;
//            
//
//            var crossed = _visGraphVerticesTree.GetAllIntersecting(rect);
//            if (crossed.Length == 0) {
//                return null;
//            }
//            double dist = double.PositiveInfinity;
//            for (int i = 0; i < crossed.Length; i++) {
//                var vv = crossed[i];
//                var vvDist = (vv.Point - p).LengthSquared;
//                if (vvDist < dist) {
//                    dist = vvDist;
//                    v = vv;
//                }
//            }
//            return v;
//        }

        void RegisterInTree(VisibilityVertex vv) {
            Rectangle rect = new Rectangle(vv.Point);
            if (_visGraphVerticesTree.OneIntersecting(rect, out VisibilityVertex? treeVv))
            {
                Debug.Assert(treeVv == vv);
                return;
            }
            _visGraphVerticesTree.Add(rect, vv);
        }

        internal bool EdgeIsPassable(VisibilityEdge e) => ((!e.Source.IsTerminal) && (!e.Target.IsTerminal)) ||
                   (e.Source.IsShortestPathTerminal || e.Target.IsShortestPathTerminal);

        internal void ClearUsedEdges() => _usedEdges.Clear();

        internal void DiminishUsed(Point a, Point b) {
            VisibilityEdge e = VisGraph.FindEdge(a, b);
            if (e == null)
            {
                return;
            }

            if (!_usedEdges.TryGetValue(e, out int usage))
            {
                return;
            }

            if (usage > 0)
            {
                _usedEdges[e]--;
            }
        }

        /// <summary>
        ///  debug
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SymmetricSegment> UsedEdges() => _usedEdges.Where(p => p.Value > 0).Select(p => p.Key).Select(e => new SymmetricSegment(e.SourcePoint, e.TargetPoint));

        public IEnumerable<SymmetricSegment> EdgesOnOldTrajectories()
        {
            List<SymmetricSegment> segs = [];
            foreach (VisibilityEdge edge in _edgesOnOldTrajectories)                
            {
                segs.Add(new SymmetricSegment(edge.SourcePoint, edge.TargetPoint));
            }
            return segs;
        }

        public List<SymmetricSegment> SegmentsNotOnOldTrajectories() {
            List<SymmetricSegment> segs = [];
            foreach (VisibilityEdge edge in VisGraph.Edges) {
                if (!_edgesOnOldTrajectories.Contains(edge)) {
                    segs.Add(new SymmetricSegment(edge.SourcePoint, edge.TargetPoint));
                }                
            }
            return segs;
        }
    }
}
