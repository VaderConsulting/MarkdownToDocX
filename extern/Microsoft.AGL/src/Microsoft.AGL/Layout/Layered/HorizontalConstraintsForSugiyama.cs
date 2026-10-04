using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.GraphAlgorithms;
using Microsoft.AGL.Core.GraphAlgorithms.CycleRemoval;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.Layered {
    internal class HorizontalConstraintsForSugiyama {
        readonly Set<Tuple<Node, Node>> leftRightConstraints = [];
        readonly Set<Tuple<Node, Node>> leftRightNeighbors = [];

        /// <summary>
        /// node is mapped to the block root
        /// </summary>
        readonly Dictionary<int, int> nodeToBlockRoot = [];

        readonly Set<Tuple<Node, Node>> upDownVerticalConstraints = [];

        /// <summary>
        /// The right most node to the left of the  block is called a block root. The root does not belong to its block.
        /// </summary>
        internal Dictionary<int, List<int>> BlockRootToBlock = [];

        internal Set<Tuple<int, int>> LeftRighInts;

        /// <summary>
        /// the set of integer pairs (i,j) such that i is a left neighbor of j
        /// </summary>
        internal Set<Tuple<int, int>> LeftRightIntNeibs;

        internal Set<Tuple<int, int>> VerticalInts;
        Dictionary<Node, int> nodeIdToIndex;

        internal Set<Tuple<Node, Node>> LeftRightNeighbors => leftRightNeighbors;

        internal Set<Tuple<Node, Node>> UpDownVerticalConstraints => upDownVerticalConstraints;

        internal Set<Tuple<Node, Node>> LeftRightConstraints => leftRightConstraints;

        internal bool IsEmpty => LeftRightNeighbors.Count == 0 && UpDownVerticalConstraints.Count == 0 &&
                       LeftRightConstraints.Count == 0;

        internal void Clear() {}


        internal void AddSameLayerNeighbors(List<Node> neighbors) {
            for (int i = 0; i < neighbors.Count - 1; i++)
                AddSameLayerNeighborsPair(neighbors[i], neighbors[i + 1]);
        }

        internal void AddSameLayerNeighborsPair(Node leftNode, Node rightNode) => LeftRightNeighbors.Insert(new Tuple<Node, Node>(leftNode, rightNode));


        int NodeToBlockRootSoft(int i) {
            if (nodeToBlockRoot.TryGetValue(i, out int blockRoot))
                return blockRoot;
            return i;
        }


        void CreateMappingOfNeibBlocks() {
            BasicGraph<IntPair> graph = BasicGraphFromLeftRightIntNeibs();
            for (int root = 0; root < graph.NodeCount; root++)
                if (graph.InEdges(root).Count == 0 && !nodeToBlockRoot.ContainsKey(root)) {
                    List<int> block = new List<int>();
                    int current = root;
                    for (IList<IntPair> outEdges = graph.OutEdges(current); outEdges.Count > 0;
                         outEdges = graph.OutEdges(current)) {
                        current = outEdges[0].Second;
                        block.Add(current);
                        nodeToBlockRoot[current] = root;
                    }
                    if (block.Count > 0)
                        BlockRootToBlock[root] = block;
                }
        }


        /*
                static void RemoveForks(Set<Tuple<int,int>> couples){
                    var graph = BasicGraphFromIntCouples(couples);
                    for (int i = 0; i < graph.NodeCount; i++) {
                        bool fork=false;
                        foreach (var edge in graph.OutEdges(i)) {
                            if (fork)
                                couples.Remove(new Tuple<int, int>(edge.First, edge.Second));
                            else
                                fork = true;
                        }
                        fork = false;
                        foreach (var edge in graph.InEdges(i)) {
                            if (fork)
                                couples.Remove(new Tuple<int, int>(edge.First, edge.Second));
                            else
                                fork = true;
                        }
                    }
                }
        */

        BasicGraph<IntPair> BasicGraphFromLeftRightIntNeibs() => new BasicGraph<IntPair>(from p in LeftRightIntNeibs select new IntPair(p.Item1, p.Item2));

        int NodeIndex(Node node) {
            if (nodeIdToIndex.TryGetValue(node, out int index))
                return index;
            return -1;
        }

        internal void PrepareForOrdering(Dictionary<Node, int> nodeToIndexParameter, int[] yLayers) {
            nodeIdToIndex = nodeToIndexParameter;
            MapNodesToToIntegers(yLayers);
        
            CreateMappingOfNeibBlocks();
            LiftLeftRightRelationsToNeibBlocks();
            //MakeUpDownRelationsMonotone(yLayers);
        }

        //see UpDownMonotone.png
//        void MakeUpDownRelationsMonotone(int[] yLayers) {
//            BasicGraph<IntPair> upDownGraph = new BasicGraph<IntPair>(from c in this.verticalInts select new IntPair(c.First,c.Second));
//            List<Tuple<int, int>> upDownToRemove = new List<Tuple<int, int>>();
//            foreach (IEnumerable<int> componentNodes in ConnectedComponentCalculator<IntPair>.GetComponents(GraphOfLeftRightRelations())) {
//                ResolveConflictsUboveComponent(upDownGraph, componentNodes, upDownToRemove, yLayers);
//                ResolveConflictsBelowComponent(upDownGraph, componentNodes, upDownToRemove, yLayers);
//            }
//
//            foreach (var v in upDownToRemove)
//                this.verticalInts.Remove(v);
//        }
        //makes left-right relations to be between neighb blocks and removes cycles in these relations
        void LiftLeftRightRelationsToNeibBlocks() {
            LeftRighInts = new Set<Tuple<int, int>>(from p in leftRightConstraints
                                                     let ip =
                                                         new Tuple<int, int>(NodeIndex(p.Item1), NodeIndex(p.Item2))
                                                     where ip.Item1 != -1 && ip.Item2 != -1
                                                     let ipb =
                                                         new Tuple<int, int>(NodeToBlockRootSoft(ip.Item1),
                                                                              NodeToBlockRootSoft(ip.Item2))
                                                     where ipb.Item1 != ipb.Item2
                                                     select ipb);
            IEnumerable<IEdge> feedbackSet =
                CycleRemoval<IntPair>.GetFeedbackSet(
                    new BasicGraph<IntPair>(from p in LeftRighInts select new IntPair(p.Item1, p.Item2)));
            foreach (IntPair ip in feedbackSet)
                LeftRighInts.Remove(new Tuple<int, int>(ip.First, ip.Second));
        }

        void MapNodesToToIntegers(int[] yLayers) {
            LeftRightIntNeibs = new Set<Tuple<int, int>>(from p in LeftRightNeighbors
                                                          let left = NodeIndex(p.Item1)
                                                          where left != -1
                                                          let right = NodeIndex(p.Item2)
                                                          where right != -1
                                                          select new Tuple<int, int>(left, right));

            //as we follow yLayers there will not be cycles in verticalIntConstraints
            VerticalInts = new Set<Tuple<int, int>>(from p in UpDownVerticalConstraints
                                                     let upper = NodeIndex(p.Item1)
                                                     where upper != -1
                                                     let lower = NodeIndex(p.Item2)
                                                     where lower != -1
                                                     where yLayers[upper] > yLayers[lower]
                                                     select new Tuple<int, int>(upper, lower));
        }
    }
}