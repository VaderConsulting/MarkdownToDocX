using System;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Routing.Visibility;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout
{
    /// <summary>
    /// sets zoom levels for LgNodeInfos
    /// </summary>
    internal class DeviceIndependendZoomCalculatorForNodes : IZoomLevelCalculator
    {
        public Func<Node, LgNodeInfo> NodeToLgNodeInfo;
        readonly int maxAmountPerTile;
        public GeometryGraph Graph { get; set; }
        public LgLayoutSettings Settings { get; set; }
        public List<LgNodeInfo> SortedLgNodeInfos => sortedLgNodeInfos;

        readonly List<int> _levelNodeCounts = [];

        public List<int> LevelNodeCounts => _levelNodeCounts;

        int unassigned;
        List<LgNodeInfo> sortedLgNodeInfos;
        int zoomLevel;
        internal DeviceIndependendZoomCalculatorForNodes(
            Func<Node, LgNodeInfo> nodeToLgNodeInfo, GeometryGraph graph, LgLayoutSettings settings, int maxAmountPerTile)
        {
            NodeToLgNodeInfo = nodeToLgNodeInfo;
            this.maxAmountPerTile = maxAmountPerTile;
            Graph = graph;
            Settings = settings;
            unassigned = graph.Nodes.Count;
        }

        /// <summary>
        /// We expect that the node Ranks are set before the method call.
        /// </summary>
        public void Run() {
            sortedLgNodeInfos = GetSortedLgNodeInfos();
            Graph.UpdateBoundingBox();
            double gridSize = Math.Max(Graph.Width, Graph.Height);
            zoomLevel = 1;

            while ( SomeNodesAreNotAssigned()) {
                Console.WriteLine("zoom level = {0} with the grid size = {1}", zoomLevel, gridSize);
                DrawNodesOnLevel(gridSize);
                zoomLevel *= 2;
                gridSize /= 2;
            }
        }

        internal static Tuple<int, int> PointToTuple(Point graphLeftBottom, Point point, double gridSize)
        {
            double dx = point.X - graphLeftBottom.X;
            double dy = point.Y - graphLeftBottom.Y;

            return new Tuple<int, int>((int)(dx / gridSize), (int)(dy / gridSize));
        }


        void DrawNodesOnLevel(double gridSize) {
            Dictionary<Tuple<int, int>, int> tileTable = new Dictionary<Tuple<int, int>, int>();
            for (int i = 0; i < sortedLgNodeInfos.Count; i++) {
                LgNodeInfo ni = sortedLgNodeInfos[i];
                Tuple<int, int> tuple = PointToTuple(Graph.LeftBottom, ni.Center, gridSize);
                if (!tileTable.ContainsKey(tuple))
                {
                    tileTable[tuple] = 0;
                }

                int countForTile = tileTable[tuple]++ + 1;
                if (countForTile > maxAmountPerTile) {
                    _levelNodeCounts.Add(i);
                    break;
                }

                if (ni.ZoomLevelIsNotSet) {
                    ni.ZoomLevel = zoomLevel;
                    unassigned--;
                }

                if (unassigned == 0) {
                    _levelNodeCounts.Add(i + 1);
                    break;
                }
            }

        }

        bool SomeNodesAreNotAssigned() => unassigned > 0;

        static internal double GetDistBetweenBoundingBoxes(Node source, Node target)
        {
            Rectangle sb = source.BoundingBox;
            Rectangle tb = target.BoundingBox;
            if (sb.Intersects(tb))
            {
                return 0;
            }

            Polygon spolygon = PolygonFromBox(sb);
            Polygon tpolygon = PolygonFromBox(tb);
            return Polygon.Distance(spolygon, tpolygon);
        }

        static Polygon PolygonFromBox(Rectangle sb)
        {
            Polygon spolygon =
                new Polygon(new Polyline(sb.LeftBottom, sb.LeftTop, sb.RightTop, sb.RightBottom) { Closed = true });
            return spolygon;
        }
        /*
                void DebugShow(LgNodeInfo[] lgNodeInfoArray) {
                    var delx = 1000.0/lgNodeInfoArray.Length;
                    var scaleMult = 5000.0;
                    var l = new List<DebugCurve>();
                    for (int i = 0; i < lgNodeInfoArray.Length; i++) {
                        l.Add(new DebugCurve(new Ellipse(1,1,new Point(i*delx,lgNodeInfoArray[i].Rank*scaleMult))));
                    }
                    LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(l);
                }*/

        List<LgNodeInfo> GetSortedLgNodeInfos()
        {
            List<LgNodeInfo> ret = Graph.Nodes.Select(n => NodeToLgNodeInfo(n)).ToList();
            ret.Sort((a, b) => b.Rank.CompareTo(a.Rank));
            return ret;
        }


#if DEBUG


        //        void ShowZoomLevels() {
        //            var l = new List<DebugCurve>();
        //            foreach (LgNodeInfo node in Graph.Nodes.Select(n => NodeToLgNodeInfo(n))) {
        //                l.Add(new DebugCurve(200, 1, "blue", node.BoundaryCurve, String.Format("{0:###.#}", node.ZoomLevel)));
        //                l.Add(new DebugCurve(100, 1, "black", node.DominatedRect.Perimeter()));
        //                l.Add(new DebugCurve(new LineSegment(node.BoundaryCurve[0], node.DominatedRect.Center)));
        //            }
        //            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(l);
        //        }
#endif




    }
}