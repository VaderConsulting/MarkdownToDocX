using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;
using MermaidRecognition.Layout.Sugiyama.Core.Routing;
using MermaidRecognition.Layout.Sugiyama.Miscellaneous.Ranking;
using MermaidRecognition.Layout.Sugiyama.Routing;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    internal class LevelCalculator { 

        static void AssignEdges(LgData lgData, IZoomLevelCalculator nodeZoomLevelCalculator) {
            AssignEdgesToLevels(lgData, nodeZoomLevelCalculator);
            LgEdgeInfo[] edgeInfos = SortEdgeInfosByZoomLevel(lgData);

            foreach (int nodeCountOnLevel in nodeZoomLevelCalculator.LevelNodeCounts) {
                bool finished = AddLevel(lgData, nodeZoomLevelCalculator, nodeCountOnLevel, edgeInfos);
                if (finished)
                    break;
            }
        }

        static bool AddLevel(LgData lgData, IZoomLevelCalculator nodeZoomLevelCalculator, int nodeCountOnLevel, LgEdgeInfo[] edgeInfos) {
            int zoomLevel = (int) nodeZoomLevelCalculator.SortedLgNodeInfos[nodeCountOnLevel - 1].ZoomLevel;
            List<LgEdgeInfo> edgeInfosOfLevel = edgeInfos.TakeWhile(ei => ei.ZoomLevel <= zoomLevel).ToList();
            lgData.AddLevel();
            return edgeInfosOfLevel.Count() == lgData.GeometryEdgesToLgEdgeInfos.Count;
        }

        static LgEdgeInfo[] SortEdgeInfosByZoomLevel(LgData lgData) {
            LgEdgeInfo[] edgeInfos = lgData.GeometryEdgesToLgEdgeInfos.Values.ToArray();
            Array.Sort(edgeInfos, (a, b) => a.ZoomLevel.CompareTo(b.ZoomLevel));
            return edgeInfos;
        }

        static void AssignEdgesToLevels(LgData lgData, IZoomLevelCalculator nodeZoomLevelCalculator) {
            foreach (int nodeCountOnLevel in nodeZoomLevelCalculator.LevelNodeCounts)
                EdgePicker.SetEdgeInfosZoomLevelsAndIcreaseRanks(lgData, nodeZoomLevelCalculator, nodeCountOnLevel);
        }


        internal static void SetNodeZoomLevelsAndRouteEdgesOnLevels(
            LgData lgData,
            GeometryGraph mainGeometryGraph,
            LgLayoutSettings lgLayoutSettings) {

            DeviceIndependendZoomCalculatorForNodes nodeZoomLevelCalculator =
                new DeviceIndependendZoomCalculatorForNodes(node => lgData.GeometryNodesToLgNodeInfos[node],
                                                            mainGeometryGraph, lgLayoutSettings, lgLayoutSettings.MaxNumberOfNodesPerTile);
            nodeZoomLevelCalculator.Run();
            lgData.SortedLgNodeInfos = nodeZoomLevelCalculator.SortedLgNodeInfos;
            lgData.LevelNodeCounts = nodeZoomLevelCalculator.LevelNodeCounts;
            AssignEdges(lgData, nodeZoomLevelCalculator);
        }

        internal static void RankGraph(LgData lgData, GeometryGraph mainGeometryGraph) {
//fromDrawingToEdgeInfo = new Dictionary<ICurve, LgEdgeInfo>();
            foreach (GeometryGraph connectedGraph in lgData.ConnectedGeometryGraphs)
                RankTheGraph(lgData, mainGeometryGraph, connectedGraph);

            UpdateRanksOfClusters(lgData);
        }

        static void UpdateRanksOfClusters(LgData lgData) {
            foreach (LgNodeInfo lgInfo in lgData.GeometryNodesToLgNodeInfos.Values) {
                foreach (Cluster cluster in lgInfo.GeometryNode.ClusterParents) {
                    if (lgData.GeometryNodesToLgNodeInfos.TryGetValue(cluster, out LgNodeInfo? clusterLgInfo))
                        if (clusterLgInfo.Rank < lgInfo.Rank)
                            clusterLgInfo.Rank = lgInfo.Rank;
                }
            }
        }


        static void RankTheGraph(LgData lgData,
                                          GeometryGraph mainGeometryGraph, GeometryGraph geomGraph) {

            Node[] nodeArray = geomGraph.Nodes.ToArray();
            GeometryGraph flatGraph = new GeometryGraph {
                Nodes = new SimpleNodeCollection(nodeArray),
                Edges =
                    new SimpleEdgeCollection(
                        geomGraph.Edges.Where(e => !(e.Source is Cluster) && !(e.Target is Cluster)))
            };

            double[] pageRank = Centrality.PageRank(flatGraph, 0.85, false);
            double normalizer = (double)geomGraph.Nodes.Count/mainGeometryGraph.Nodes.Count;
            for (int i = 0; i < nodeArray.Length; i++) {
                Node node = nodeArray[i];
                Debug.Assert(node != mainGeometryGraph.RootCluster);
                lgData.GeometryNodesToLgNodeInfos[node].Rank = normalizer*pageRank[i];
            }
        }
    }
}