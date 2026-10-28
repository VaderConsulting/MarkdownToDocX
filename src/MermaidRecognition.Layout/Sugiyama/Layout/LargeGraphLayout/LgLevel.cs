using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.RTree;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using SymmetricSegment = MermaidRecognition.Layout.Sugiyama.Core.DataStructures.SymmetricTuple<MermaidRecognition.Layout.Sugiyama.Core.Geometry.Point>;
namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    /// <summary>
    /// class keeping a level info
    /// </summary>
    public class LgLevel {
        internal readonly Dictionary<SymmetricSegment, Rail> _railDictionary =
            [];

        internal Dictionary<SymmetricSegment, Rail> RailDictionary => _railDictionary;

        internal readonly Dictionary<Edge, Set<Rail>> _railsOfEdges = [];
        internal Set<Rail> HighlightedRails = [];
        internal readonly int ZoomLevel;
        readonly GeometryGraph _geomGraph;
        internal RTree<Rail> _railTree = new RTree<Rail>();
        internal RTree<Rail> RailTree => _railTree;

        RTree<LgNodeInfo> _nodeInfoTree = new RTree<LgNodeInfo>();

        internal readonly Dictionary<Edge, List<Rail>> _orderedRailsOfEdges = [];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="zoomLevel"></param>
        /// <param name="geomGraph">needed only for statistics</param>
        internal LgLevel(int zoomLevel, GeometryGraph geomGraph) {
            _geomGraph = geomGraph;
            ZoomLevel = zoomLevel;
        }

        internal RTree<LgNodeInfo> NodeInfoTree => _nodeInfoTree;


        internal void CreateEmptyRailTree() => _railTree = new RTree<Rail>();

        //public void CreateRailTree() {
        //    RailTree = new RTree<Rail>(
        //        RailDictionary.Values.Select(rail => new KeyValuePair<Rectangle, Rail>(rail.BoundingBox, rail)));
        //}

        /// <summary>
        /// get endpoint tuples of all rails
        /// </summary>
        /// <returns></returns>
        public List<SymmetricSegment> GetAllRailsEndpoints() {
            List<SymmetricSegment> endpts = [];
            foreach (Rail rail in _railTree.GetAllLeaves())
            {
                if (rail.GetStartEnd(out Point p0, out Point p1))
                {
                    endpts.Add(new SymmetricSegment(p0, p1));
                }
            }
            return endpts;
        }

        public List<Rail> FetchOrCreateRailSequence(List<Point> path) {
            List<Rail> rails = [];
            for (int i = 0; i < path.Count - 1; i++) {
                Rail rail = FindOrCreateRail(path[i], path[i + 1]);
                if (rail == null)
                {
                    continue;
                }

                rails.Add(rail);
            }
            return rails;
        }

        public Rail FindOrCreateRail(Point s, Point t) {
            Point p0 = s;
            Point p1 = t;

            SymmetricSegment t1 = new SymmetricSegment(p0, p1);
            if (RailDictionary.TryGetValue(t1, out Rail? rail))
            {
                return rail;
            }

            SymmetricSegment t2 = new SymmetricSegment(p1, p0);
            if (RailDictionary.TryGetValue(t2, out rail))
            {
                return rail;
            }

            // no rail exists // roman: please check that this code really can be commented out and does need to be fixed instead 
            /*
             * var q0 = VisGraph.GetPointOfVisGraphVertex(s);
            var q1 = VisGraph.GetPointOfVisGraphVertex(t);
            if (q0 == null || q1 == null)
            {
                //no visgraph vertex found
            }
            else
            {
                var edge = VisGraph.FindEdge(q0.Point, q1.Point);
            }*/
            return CreateRail(p0, p1);
        }

        public Rail CreateRail(Point ep0, Point ep1) {
            SymmetricSegment st = new SymmetricSegment(ep0, ep1);
            if (RailDictionary.TryGetValue(st, out Rail? rail))
            {
                return rail;
            }
            LineSegment ls = new LineSegment(ep0, ep1);
            rail = new Rail(ls, ZoomLevel);
            RailTree.Add(rail.BoundingBox, rail);
            RailDictionary.Add(st, rail);
            return rail;
        }

        public Rail FindRail(Point s, Point t) {
            Point p0 = s;
            Point p1 = t;
            SymmetricSegment ss = new SymmetricSegment(p1, p0);
            RailDictionary.TryGetValue(ss, out Rail? rail);
            return rail;
        }
        
        internal IEnumerable<Rail> GetRailsIntersectingRect(Rectangle visibleRectange) {
            Set<Rail> ret = [];
            foreach (Rail rail in _railTree.GetAllIntersecting(visibleRectange))
            {
                ret.Insert(rail);
            }

            return ret;
        }

        internal List<Edge> GetEdgesPassingThroughRail(Rail rail) => [.. (from kv in _railsOfEdges where kv.Value.Contains(rail) select kv.Key)];

        public Rail RemoveRailFromRtree(Rail rail) => _railTree.Remove(rail.BoundingBox, rail);

        /// <summary>
        /// try adding single rail to dictionary
        /// </summary>
        /// <param name="rail"></param>
        /// <returns>true iff the rail does not belong to _railDictionary</returns>
        public bool AddRailToDictionary(Rail rail) {
            if (!rail.GetStartEnd(out Point p0, out Point p1))
            {
                return false;
            }

            SymmetricSegment st = new SymmetricSegment(p0, p1);
            if (!_railDictionary.ContainsKey(st)) {
                _railDictionary.Add(st, rail);
                return true;
            }
            return false;
        }

        public void AddRailToRtree(Rail rail) {
            if (!rail.GetStartEnd(out Point p0, out Point p1))
            {
                return;
            }

            if (_railTree.Contains(new Rectangle(p0, p1), rail))
            {
                return;
            }

            _railTree.Add(new Rectangle(p0, p1), rail);
        }

        public void RemoveRailFromDictionary(Rail rail) {
            if (!rail.GetStartEnd(out Point p0, out Point p1))
            {
                return;
            }

            _railDictionary.Remove(new SymmetricSegment(p0, p1));
        }

        #region Statistics

        internal void RunLevelStatistics(IEnumerable<Node> nodes) {
            Console.WriteLine("running stats");

            foreach (Rail rail in _railDictionary.Values)
            {
                CreateStatisticsForRail(rail);
            }

            RunStatisticsForNodes(nodes);

            double numberOfTiles = (double) ZoomLevel*ZoomLevel;
            double averageRailsForTile = 0;
            double averageVerticesForTile = 0;

            int maxVerticesPerTile = 0;
            int maxRailsPerTile = 0;
            int maxTotalPerTile = 0;

            foreach (TileStatistic tileStatistic in tileTableForStatistic.Values) {
                averageVerticesForTile += tileStatistic.vertices/numberOfTiles;
                averageRailsForTile += tileStatistic.rails/numberOfTiles;
                if (maxRailsPerTile < tileStatistic.rails)
                {
                    maxRailsPerTile = tileStatistic.rails;
                }

                if (maxVerticesPerTile < tileStatistic.vertices)
                {
                    maxVerticesPerTile = tileStatistic.vertices;
                }

                if (maxTotalPerTile < tileStatistic.vertices + tileStatistic.rails)
                {
                    maxTotalPerTile = tileStatistic.vertices + tileStatistic.rails;
                }
            }

            Console.WriteLine("level {0}: average rails per tile {1}\n" +
                              "average verts per tile {2}, total average per tile {1}.\n", ZoomLevel,
                averageRailsForTile, averageVerticesForTile);

            Console.WriteLine("max rails per tile {0}\n" +
                              "max verts per tile {1}, total max per tile {2}.\n", maxRailsPerTile,
                maxVerticesPerTile, maxTotalPerTile);

            Console.WriteLine("done with stats");
        }

        void RunStatisticsForNodes(IEnumerable<Node> nodes) {
            foreach (Node node in nodes)
            {
                CreateStatisticsForNode(node);
            }
        }

        void CreateStatisticsForNode(Node node) {
            foreach (TileStatistic tile in GetCurveTiles(node.BoundaryCurve))
            {
                tile.vertices++;
            }
        }

        void CreateStatisticsForRail(Rail rail) {
            if (rail.Geometry is Arrowhead arrowhead)
            {
                CreateStatisticsForArrowhead(arrowhead);
            }
            else
            {
                foreach (TileStatistic t in GetCurveTiles(rail.Geometry as ICurve))
                {
                    t.rails++;
                }
            }
        }

        void CreateStatisticsForArrowhead(Arrowhead arrowhead) {
            TileStatistic tile = GetOrCreateTileStatistic(arrowhead.TipPosition);
            tile.rails++;
        }

        TileStatistic GetOrCreateTileStatistic(Point p) {
            Tuple<int, int> t = DeviceIndependendZoomCalculatorForNodes.PointToTuple(_geomGraph.LeftBottom, p,
                GetGridSize());
            if (tileTableForStatistic.TryGetValue(t, out TileStatistic? ts))
            {
                return ts;
            }

            tileTableForStatistic[t] = ts = new TileStatistic {rails = 0, vertices = 0};
            return ts;
        }

        IEnumerable<TileStatistic> GetCurveTiles(ICurve curve) {
            Set<TileStatistic> tiles = [];
            const int n = 64;
            double s = curve.ParStart;
            double e = curve.ParEnd;
            double d = (e - s)/(n - 1);
            for (int i = 0; i < 64; i++) {
                double t = s + (i*d);
                TileStatistic ts = GetOrCreateTileStatistic(curve[t]);
                tiles.Insert(ts);
            }
            return tiles;
        }

        class TileStatistic {
            public int vertices;
            public int rails;
        }

        readonly Dictionary<Tuple<int, int>, TileStatistic> tileTableForStatistic =
            [];

        double GetGridSize() => Math.Max(_geomGraph.Width, _geomGraph.Height) / ZoomLevel;

        #endregion


        internal void AddRail(Rail rail) {
            if (AddRailToDictionary(rail))
            {
                AddRailToRtree(rail);
            }
        }

        public void ClearRailTree() => _railTree.Clear();

        public void CreateNodeTree(IEnumerable<LgNodeInfo> nodeInfos, double nodeDotWidth) {
            foreach (LgNodeInfo n in nodeInfos)
            {
                NodeInfoTree.Add(new RectangleNode<LgNodeInfo>(n,
                    new Rectangle(new Size(nodeDotWidth, nodeDotWidth),n.Center)));
            }
        }

        public IEnumerable<Node> GetNodesIntersectingRect(Rectangle visibleRectangle) => NodeInfoTree.GetAllIntersecting(visibleRectangle).Select(n => n.GeometryNode);

        public bool RectIsEmptyOnLevel(Rectangle tileBox) => !NodeInfoTree.OneIntersecting(tileBox, out LgNodeInfo? lg);

        internal void RemoveFromRailEdges(List<SymmetricTuple<LgNodeInfo>> removeList) {
            foreach (SymmetricTuple<LgNodeInfo> symmetricTuple in removeList)
            {
                RemoveTupleFromRailEdges(symmetricTuple);
            }
        }

        void RemoveTupleFromRailEdges(SymmetricTuple<LgNodeInfo> tuple) {
            Node a = tuple.A.GeometryNode;
            Node b = tuple.B.GeometryNode;
            foreach (Edge edge in EdgesBetween(a, b))
            {
                _railsOfEdges.Remove(edge);
            }
        }

        IEnumerable<Edge> EdgesBetween(Node a, Node b) {
            foreach (Edge? e in a.InEdges.Where(e => e.Source == b))
            {
                yield return e;
            }

            foreach (Edge? e in a.OutEdges.Where(e => e.Target == b))
            {
                yield return e;
            }
        }
    }
}