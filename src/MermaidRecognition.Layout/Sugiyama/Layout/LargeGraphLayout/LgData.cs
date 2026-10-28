using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;
using MermaidRecognition.Layout.Sugiyama.Miscellaneous.Ranking;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    /// <summary>
    /// data for large graph browsing
    /// </summary>
    public class LgData {
        readonly List<GeometryGraph> _connectedComponents = [];
        internal Dictionary<Node, LgNodeInfo> GeometryNodesToLgNodeInfos;
        internal readonly Dictionary<Edge, LgEdgeInfo> GeometryEdgesToLgEdgeInfos = [];

        readonly Set<Edge> _selectedEdges=[];
        Set<LgNodeInfo> _selectedNodeInfos = [];
        readonly List<LgLevel> _levels = [];

        internal readonly List<LgSkeletonLevel> SkeletonLevels = [];

        /// <summary>
        /// the list of levels
        /// </summary>
        public IList<LgLevel> Levels => _levels;

        internal LgData(GeometryGraph mainGeomGraph) => this.mainGeomGraph = mainGeomGraph;



        #region Level class

        #endregion  end of Level class


        internal void AddConnectedGeomGraph(GeometryGraph geomGraph) => _connectedComponents.Add(geomGraph);

        internal IEnumerable<GeometryGraph> ConnectedGeometryGraphs => _connectedComponents;

        /// <summary>
        /// LgNodeInfos sorted by their importance
        /// </summary>
        public List<LgNodeInfo> SortedLgNodeInfos { get; set; }
        /// <summary>
        /// the last indices of nodes on levels, the indices point to SortedLgNodeInfos
        /// </summary>
        public List<int> LevelNodeCounts { get; set; }


        /*
                static void showdeletelater(ICurve curve, LinkedList<Tuple<double, Point>> samples, Point middleValue) {
                    var l = new List<DebugCurve>();
                    l.Add(new DebugCurve(100, 1, "black", curve));
                    foreach (var sample in samples)
                        l.Add(new DebugCurve( 0.1, "red", new Ellipse(1, 1, sample.Item2)));
                    l.Add(new DebugCurve(1, "blue", new Ellipse(3, 3, middleValue)));

                    LayoutAlgorithmSettings.ShowDebugCurves(l.ToArray());
                }
        */

        internal LgLevel GetCurrentLevelByScale(double zoomLevel) => _levels[GetLevelIndexByScale(zoomLevel)];

        internal LgLevel AddLevel() {
            int zoomLevel = (int) Math.Pow(2, _levels.Count());
            LgLevel level = new LgLevel(zoomLevel, mainGeomGraph);
            _levels.Add(level);
            return level;
        }

        GeometryGraph mainGeomGraph; //needed for statistice only
        
//        public void ExtractRailsFromRouting(EdgeCollection edges, int levelZoom) {
//            var level = AddLevel(levelZoom);
//            level.CreateRailTree(edges);
//
//
//
//            Console.WriteLine("segs={0} rail count = {1}",
//                              edges.Sum(
//                                  e =>
//                                  ((e.Curve is Curve) ? ((Curve) e.Curve).Segments.Count : 1) +
//                                  (e.EdgeGeometry.SourceArrowhead != null ? 1 : 0) +
//                                  (e.EdgeGeometry.TargetArrowhead != null ? 1 : 0)), level.Rails.Count());
//        }

        //        void UpdateEdgeGeometriesByLevel(int levelIndex, Rail rail) {
        //            var edgeInfo = rail.EdgeInfo;
        //            var edgeGeomsByLevels = edgeInfo.EdgeGeometriesByLevels;
        //            if (edgeGeomsByLevels == null)
        //                edgeInfo.EdgeGeometriesByLevels = edgeGeomsByLevels = new List<EdgePartialGeometryOnLevel>();
        //
        //            EdgePartialGeometryOnLevel edgePartialGeom;
        //            if (edgeInfo.EdgeGeometriesByLevels.Count < levelIndex + 1)
        //                edgeGeomsByLevels.Add(edgePartialGeom = new EdgePartialGeometryOnLevel());
        //            else
        //                edgePartialGeom = edgeGeomsByLevels[levelIndex];
        //
        //            var geom = rail.Geometry;
        //            var edgeGeometry = edgeInfo.Edge.EdgeGeometry;
        //            var arrowhead = geom as Arrowhead;
        //            if (arrowhead != null) {
        //                if (arrowhead == edgeGeometry.SourceArrowhead) {
        //                    edgePartialGeom.SourceArrowhead = arrowhead;
        //                } else {
        //                    Debug.Assert(arrowhead == edgeGeometry.TargetArrowhead);
        //                    edgePartialGeom.TargetArrowhead = arrowhead;
        //                }
        //            } else {
        //                if (edgePartialGeom.ConnectedPiecesOfCurve == null)
        //                    edgePartialGeom.ConnectedPiecesOfCurve = new List<ICurve>();
        //                edgePartialGeom.ConnectedPiecesOfCurve.Add((ICurve) geom);
        //            }
        //        }


        internal int GetLevelIndexByScale(double scale) {
            if (scale <= 1)
            {
                return 0;
            }

            if (scale >= _levels.Last().ZoomLevel)
            {
                return _levels.Count - 1;
            }

            double z = Math.Log(scale, 2);
            int ret = (int)Math.Ceiling(z);
            Debug.Assert(0 <= ret && ret < _levels.Count);
            return ret;
        }



        /*
                void RemoveRailsOfPassingEdgesFromLowerLevelsAndReduceHighlights(List<Edge> passingEdges) {
                    var dimmedRails = new Set<Rail>();
                    foreach (var edge in passingEdges) {
                        for (int i = levels.Count - 1; i >= 0; i--) {
                            var level = levels[i];                    
                            DiminishHighlightAndCollectDimmedRailOnLevel(edge, level, dimmedRails);
                        }
                    }
      
                    RemoveDimmedRailsFromLowerLevels(dimmedRails);
                    foreach (var edge in passingEdges) {
                        var ei = GeometryEdgesToLgEdgeInfos[edge];
                        for (int i = 0; i<levels.Count; i++) {
                            var level = levels[i];
                            if (ei.ZoomLevel > level.ZoomLevel) {
                                Set<Rail> railsOfEdgeOnLevel;
                                if (level._railsOfEdges.TryGetValue(edge, out railsOfEdgeOnLevel))
                                    if (railsOfEdgeOnLevel.Any(dimmedRails.Contains))
                                        level._railsOfEdges.Remove(edge);
                            }
                            else break;
                        }
                    }
                }
        */
#if DEBUG && !SILVERLIGHT && !SHARPKIT
        static void ShowDimmedRails(Set<Rail> dimmedRails) {
            List<DebugCurve> l = [];
            foreach (Rail r in dimmedRails) {
                string s = r.ToString();
                string color =
                    s.StartsWith("15") ? "green" : (s.StartsWith("1384") ? "red" : "blue");
                 l.Add(new DebugHelpers.DebugCurve(100, 1, color, r.Geometry as ICurve));
                
            }
            LayoutAlgorithmSettings.ShowDebugCurvesEnumeration(l);
        }
#endif
        //        void RemoveDimmedRailsFromLowerLevels(Set<Rail> dimmedRails) {
//            foreach(var rail in dimmedRails)
//                RemoveDimmedRailFromLowerLevels(rail);
//        }
//
//        void RemoveDimmedRailFromLowerLevels(Rail rail) {
//            Debug.Assert(rail.IsHighlighted==0);
//            for (int i = 0; i < levels.Count; i++) {
//                var level = levels[i];
//                if (level.ZoomLevel < rail.ZoomLevel) {
//                    //the rail does not belong to this level
//                    var railTuple = rail.PointTuple();
//                    var rect = new Rectangle(railTuple.Item1, railTuple.Item2);
//                    level._railTree.Remove(rect, rail);                    
//                }
//                else break;
//            }
//
//        }

//        static void DiminishHighlightAndCollectDimmedRailOnLevel(Edge edge, Level level, Set<Rail> dimmedRails) {
//            Set<Rail> railsOfEdge;
//            if (!level._railsOfEdges.TryGetValue(edge, out railsOfEdge)) return;
//            foreach (var rail in railsOfEdge) {
//                if (rail.IsHighlighted == 0) continue;
//                rail.IsHighlighted--;
//                if (rail.IsHighlighted == 0)
//                    dimmedRails.Insert(rail);
//            }
//        }



        internal void SelectEdges(List<Edge> passingEdges) {
            SelectedEdges.InsertRange(passingEdges);
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SelectEdgesOnLevel(i, passingEdges);
            }
        }

        public Set<Edge> SelectedEdges => _selectedEdges;

        internal Set<LgNodeInfo> SelectedNodeInfos { get => _selectedNodeInfos; private set => _selectedNodeInfos = value;
        }

        void SelectEdgesOnLevel(int i, List<Edge> edges) {
            LgLevel level = _levels[i];
            Set<Rail> railsToHighlight = [];
            foreach (Edge edge in edges) {
                if (!level._railsOfEdges.TryGetValue(edge, out Set<Rail>? railsOfEdge))
                {
                    LgEdgeInfo edgeInfo = GeometryEdgesToLgEdgeInfos[edge];
                    int edgeLevelIndex = (int)Math.Min(Math.Log(edgeInfo.ZoomLevel, 2), _levels.Count - 1);
                    railsOfEdge = _levels[edgeLevelIndex]._railsOfEdges[edge];
                    TransferHighlightedRails(level, edge, railsOfEdge);
                }
                else
                {
                    railsToHighlight.InsertRange(railsOfEdge);
                }
            }
            //need to analyze and highlight the new highlighted rails
            foreach (Rail rail in railsToHighlight) {
                if (!level.HighlightedRails.Contains(rail)) {
                    rail.IsHighlighted = true;
                    level.HighlightedRails.Insert(rail);
                }
            }

        }

        /// <summary>
        /// gets all rails corresponding to edges on given level.
        /// </summary>
        /// <param name="i"></param>
        /// <param name="edges"></param>
        /// <returns></returns>
        public Set<Rail> GetRailsOfEdgesOnLevel(int i, List<Edge> edges)
        {
            LgLevel level = _levels[i];
            Set<Rail> railsToHighlight = [];
            foreach (Edge edge in edges)
            {
                if (!level._railsOfEdges.TryGetValue(edge, out Set<Rail>? railsOfEdge))
                {
                    LgEdgeInfo edgeInfo = GeometryEdgesToLgEdgeInfos[edge];
                    int edgeLevelIndex = (int)Math.Min(Math.Log(edgeInfo.ZoomLevel, 2), _levels.Count - 1);
                    railsOfEdge = _levels[edgeLevelIndex]._railsOfEdges[edge];
                    TransferHighlightedRails(level, edge, railsOfEdge); // roman: todo: necessary?
                }
                else
                {
                    railsToHighlight.InsertRange(railsOfEdge);
                }
            }
            return railsToHighlight;
        }

        public Set<Rail> GetRailsOfEdgeOnLevel(int i, Edge edge)
        {
            LgLevel level = _levels[i];
            Set<Rail> railsToHighlight = [];

            if (level._railsOfEdges.TryGetValue(edge, out Set<Rail>? railsOfEdge))
            {
                railsToHighlight.InsertRange(railsOfEdge);
            }
            //else
            //{
            //    var edgeInfo = GeometryEdgesToLgEdgeInfos[edge];
            //    int edgeLevelIndex = (int)Math.Min(Math.Log(edgeInfo.ZoomLevel, 2), levels.Count - 1);
            //    railsOfEdge = levels[edgeLevelIndex]._railsOfEdges[edge];
            //    TransferHighlightedRails(level, edge, railsOfEdge); // roman: todo: necessary?
            //}

            return railsToHighlight;
        }

        static void TransferHighlightedRails(LgLevel level, Edge edge, Set<Rail> railsOfEdge) {
            //need to remove those rails later, when putting them off
            foreach (Rail rail in railsOfEdge)
            {
                AddRailToRailTreeOfLowerLevel(rail, level);
            }

            level._railsOfEdges[edge] = railsOfEdge;
            level.HighlightedRails.InsertRange(railsOfEdge);
        }

/*
        void HiglightEdge(Edge edge) {
            Set<Rail> railsOfEdge = null;
            //adding highlight for edge rails in each level
            for (int i = levels.Count - 1; i >= 0; i--) {                
                var level = levels[i];

                Set<Rail> railsOfEdgeOfLevelWithHigherZoom;
                if (level._railsOfEdges.TryGetValue(edge, out railsOfEdgeOfLevelWithHigherZoom)) {
                    railsOfEdge = railsOfEdgeOfLevelWithHigherZoom;
                }
                else {
                    //the edge is not represented at the level, so we are reusing the rails from the level above
                    level._railsOfEdges[edge] = railsOfEdge;
                    foreach (var rail1 in railsOfEdge)
                        AddRailToRailTreeOfLowerLevel(rail1, level);
                }
                foreach (var rail1 in railsOfEdge) //highlight every rail: does not matter new or old
                    rail1.HiglightCount ++;
            }
        }
*/

        static void AddRailToRailTreeOfLowerLevel(Rail rail, LgLevel lowerLevel) {
            Tuple<Point, Point> pt = rail.PointTuple();
            Rectangle box = new Rectangle(pt.Item1, pt.Item2);
            if (!lowerLevel._railTree.Contains(box, rail))
            {
                lowerLevel._railTree.Add(box, rail);
            }
        }


        internal double GetMaximalZoomLevel() {
            if (_levels == null || _levels.Count == 0)
            {
                return 1;
            }

            return _levels.Last().ZoomLevel;
        }

        internal void PutOffEdgesPassingThroughTheRail(Rail rail) {
            LgLevel railLevel = _levels[(int)Math.Log(rail.ZoomLevel, 2)];
            List<Edge> passingEdges = railLevel.GetEdgesPassingThroughRail(rail);
            UnselectEdges(passingEdges);
        }

        public void UnselectEdges(List<Edge> edgesToPutOff) {
            Set<Edge> edgesToPutoffSet = new Set<Edge>(edgesToPutOff);
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                PutOffEdgesOnLevel(i, edgesToPutoffSet);
            }

            foreach (Edge e in edgesToPutOff)
            {
                SelectedEdges.Remove(e);
            }
        }

        void PutOffEdgesOnLevel(int i, Set<Edge> edgesToPutOff) {
            LgLevel level = _levels[i];
            Set<Rail> railsThatShouldBeHiglighted = GetRailsThatShouldBeHighlighted(level, edgesToPutOff);

            foreach (Rail rail in level.HighlightedRails) {
                if (!railsThatShouldBeHiglighted.Contains(rail)) {
                    if (RailBelongsToLevel(level, rail))
                    {
                        rail.IsHighlighted = false;
                    }
                    else {
                        Tuple<Point, Point> railTuple = rail.PointTuple();
                        Rectangle rect = new Rectangle(railTuple.Item1, railTuple.Item2);
                        level._railTree.Remove(rect, rail);
                    }
                        
                }
            }
            foreach (Edge edge in edgesToPutOff) {
                LgEdgeInfo lgEdgeInfo = GeometryEdgesToLgEdgeInfos[edge];
                if (lgEdgeInfo.ZoomLevel > level.ZoomLevel)
                {
                    level._railsOfEdges.Remove(edge);
                }
            }

            level.HighlightedRails = railsThatShouldBeHiglighted;
        }

        static bool RailBelongsToLevel(LgLevel level, Rail rail) => level.ZoomLevel >= rail.ZoomLevel;

        Set<Rail> GetRailsThatShouldBeHighlighted(LgLevel level, Set<Edge> edgesToPutOff) {
            Set<Rail> ret = [];
            foreach (Edge edge in SelectedEdges) {
                if (edgesToPutOff.Contains(edge))
                {
                    continue;
                }

                if (level._railsOfEdges.TryGetValue(edge, out Set<Rail>? railsOfEdge))
                {
                    ret.InsertRange(railsOfEdge);
                }
            }
            return ret;
        }


        public void AssembleEdgeAtLevel(LgEdgeInfo lgEi, int iLevel, Set<Rail> rails) {
            Edge edge = lgEi.Edge;

            LgLevel level = Levels[iLevel];

            level._railsOfEdges[edge] = rails;
            foreach (Rail rail in rails) {
                level.AddRail(rail);
                rail.UpdateTopEdgeInfo(lgEi);
            }
        }

        public void PutOffAllEdges() => UnselectEdges([.. SelectedEdges]);

        public void CreateLevelNodeTrees(double nodeDotWidth) {
            for (int i = 0; i < _levels.Count; i++) {
                _levels[i].CreateNodeTree(SortedLgNodeInfos.Take(LevelNodeCounts[i]), nodeDotWidth);
                nodeDotWidth /= 2;
            }
        }


        public bool NodeTreeIsCorrectOnLevel(int iLevel) => _levels[iLevel].NodeInfoTree.Count == LevelNodeCounts[iLevel];
    }
}