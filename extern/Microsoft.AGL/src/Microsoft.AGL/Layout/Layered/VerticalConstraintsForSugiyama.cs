using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.GraphAlgorithms;
using Microsoft.AGL.Core.GraphAlgorithms.CycleRemoval;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.Layered {
    /// <summary>
    /// vertical constraints for Suquiyama scheme
    /// </summary>
    internal class VerticalConstraintsForSugiyama {
        readonly Set<Node> _maxLayerOfGeomGraph = [];
        /// <summary>
        /// nodes that are pinned to the max layer
        /// </summary>
        internal Set<Node> MaxLayerOfGeomGraph => _maxLayerOfGeomGraph;

        readonly Set<Node> _minLayerOfGeomGraph = [];
        /// <summary>
        /// nodes that are pinned to the min layer
        /// </summary>
        internal Set<Node> MinLayerOfGeomGraph => _minLayerOfGeomGraph;

        Set<Tuple<Node, Node>> sameLayerConstraints = [];

        /// <summary>
        /// set of couple of nodes belonging to the same layer
        /// </summary>
        internal Set<Tuple<Node, Node>> SameLayerConstraints => sameLayerConstraints;
        Set<Tuple<Node, Node>> upDownConstraints = [];

        /// <summary>
        /// set of node couples such that the first node of the couple is above the second one
        /// </summary>
        internal Set<Tuple<Node, Node>> UpDownConstraints => upDownConstraints;
        /// <summary>
        /// pins a node to max layer
        /// </summary>
        /// <param name="node"></param>
        internal void PinNodeToMaxLayer(Node node) => MaxLayerOfGeomGraph.Insert(node);

        /// <summary>
        /// unpins a node from max layer
        /// </summary>
        /// <param name="node"></param>
        internal void UnpinNodeFromMaxLayer(Node node) => MaxLayerOfGeomGraph.Remove(node);

        /// <summary>
        /// pins a node to min layer
        /// </summary>
        /// <param name="node"></param>
        internal void PinNodeToMinLayer(Node node)
        {
            System.Diagnostics.Debug.Assert(node != null);
            MinLayerOfGeomGraph.Insert(node);
        }

        /// <summary>
        /// unpins a node from min layer
        /// </summary>
        /// <param name="node"></param>
        internal void UnpinNodeFromMinLayer(Node node) => MinLayerOfGeomGraph.Remove(node);

        internal bool IsEmpty => MaxLayerOfGeomGraph.Count == 0 && MinLayerOfGeomGraph.Count == 0 && SameLayerConstraints.Count == 0 && UpDownConstraints.Count == 0;





        internal void Clear() {
            MaxLayerOfGeomGraph.Clear(); 
            MinLayerOfGeomGraph.Clear();
            SameLayerConstraints.Clear();
            UpDownConstraints.Clear();
        }
        Set<IntPair> gluedUpDownIntConstraints = [];

        internal Set<IntPair> GluedUpDownIntConstraints { get => gluedUpDownIntConstraints; set => gluedUpDownIntConstraints = value;
        }
        Dictionary<Node, int> nodeIdToIndex;
        BasicGraph<Node, IntEdge> intGraph;
        /// <summary>
        /// this graph is obtained from intGraph by glueing together same layer vertices
        /// </summary>
        BasicGraph<IntPair> gluedIntGraph;
        int maxRepresentative;
        int minRepresentative;
        /// <summary>
        /// Maps each node participating in same layer relation its representative on the layer.
        /// </summary>
        Dictionary<int, int> sameLayerDictionaryOfRepresentatives = [];
        Dictionary<int, IEnumerable<int>> representativeToItsLayer = [];
        internal IEnumerable<IEdge> GetFeedbackSet(BasicGraph<Node, IntEdge> intGraphPar, Dictionary<Node, int> nodeIdToIndexPar) {
            nodeIdToIndex = nodeIdToIndexPar;
            intGraph = intGraphPar;
            maxRepresentative = -1;
            minRepresentative = -1;
            CreateIntegerConstraints();
            GlueTogetherSameConstraintsMaxAndMin();
            AddMaxMinConstraintsToGluedConstraints();
            RemoveCyclesFromGluedConstraints();
            return GetFeedbackSet();
        }

        private void RemoveCyclesFromGluedConstraints() {
            IEnumerable<IEdge> feedbackSet = CycleRemoval<IntPair>.
                GetFeedbackSetWithConstraints(new BasicGraph<IntPair>(GluedUpDownIntConstraints, intGraph.NodeCount), null);
            //feedbackSet contains all glued constraints making constraints cyclic
            foreach (IntPair p in feedbackSet)
                GluedUpDownIntConstraints.Remove(p);
        }

        private void AddMaxMinConstraintsToGluedConstraints() {
            if (maxRepresentative != -1)
                for (int i = 0; i < intGraph.NodeCount; i++) {
                    int j = NodeToRepr(i);
                    if (j != maxRepresentative)
                        GluedUpDownIntConstraints.Insert(new IntPair(maxRepresentative, j));
                }

            if (minRepresentative != -1)
                for (int i = 0; i < intGraph.NodeCount; i++) {
                    int j = NodeToRepr(i);
                    if (j != minRepresentative)
                        GluedUpDownIntConstraints.Insert(new IntPair(j, minRepresentative));
                }
        }

        private void GlueTogetherSameConstraintsMaxAndMin() {
            CreateDictionaryOfSameLayerRepresentatives();
            GluedUpDownIntConstraints = new Set<IntPair>(from p in UpDownInts select GluedIntPair(p));
        }

        internal IntPair GluedIntPair(Tuple<int, int> p) => new IntPair(NodeToRepr(p.Item1), NodeToRepr(p.Item2));

        private IntPair GluedIntPair(IntEdge p) => new IntPair(NodeToRepr(p.Source), NodeToRepr(p.Target));

        internal IntPair GluedIntPair(IntPair p) => new IntPair(NodeToRepr(p.First), NodeToRepr(p.Second));

        internal IntEdge GluedIntEdge(IntEdge intEdge) {
            int sourceRepr = NodeToRepr(intEdge.Source);
            int targetRepr = NodeToRepr(intEdge.Target);
            IntEdge ie = new IntEdge(sourceRepr, targetRepr)
            {
                Separation = intEdge.Separation,
                Weight = 0,
                Edge = intEdge.Edge
            };
            return ie;
        }
        

        internal int NodeToRepr(int node) {
            if (sameLayerDictionaryOfRepresentatives.TryGetValue(node, out int repr))
                return repr;
            return node;
        }

        private void CreateDictionaryOfSameLayerRepresentatives() {
            BasicGraph<IntPair> graphOfSameLayers = CreateGraphOfSameLayers();
            foreach (IEnumerable<int> comp in ConnectedComponentCalculator<IntPair>.GetComponents(graphOfSameLayers))
                GlueSameLayerNodesOfALayer(comp);
        }

        private BasicGraph<IntPair> CreateGraphOfSameLayers() => new BasicGraph<IntPair>(CreateEdgesOfSameLayers(), intGraph.NodeCount);

        private IEnumerable<IntPair> CreateEdgesOfSameLayers() {
            List<IntPair> ret = [];
            if (maxRepresentative != -1)
                ret.AddRange(from v in maxLayerInt where v != maxRepresentative select new IntPair(maxRepresentative, v));
            if (minRepresentative != -1)
                ret.AddRange(from v in minLayerInt where v != minRepresentative select new IntPair(minRepresentative, v));
            ret.AddRange(from couple in SameLayerInts select new IntPair(couple.Item1, couple.Item2));
            return ret;
        }
        /// <summary>
        /// maps all nodes of the component to one random representative
        /// </summary>
        /// <param name="sameLayerNodes"></param>
        private void GlueSameLayerNodesOfALayer(IEnumerable<int> sameLayerNodes) {
            if (sameLayerNodes.Count<int>() > 1) {
                int representative = -1;
                if (ComponentsIsMaxLayer(sameLayerNodes))
                    foreach (int v in sameLayerNodes)
                        sameLayerDictionaryOfRepresentatives[v] = representative = maxRepresentative;
                else if (ComponentIsMinLayer(sameLayerNodes))
                    foreach (int v in sameLayerNodes)
                        sameLayerDictionaryOfRepresentatives[v] = representative = minRepresentative;
                else {
                    foreach (int v in sameLayerNodes) {
                        if (representative == -1)
                            representative = v;
                        sameLayerDictionaryOfRepresentatives[v] = representative;
                    }
                }
                representativeToItsLayer[representative] = sameLayerNodes;
            }
        }

        private bool ComponentIsMinLayer(IEnumerable<int> component) => component.Contains<int>(minRepresentative);

        private bool ComponentsIsMaxLayer(IEnumerable<int> component) => component.Contains<int>(maxRepresentative);

        List<int> maxLayerInt = [];
        List<int> minLayerInt = [];
        List<Tuple<int, int>> sameLayerInts = [];

        /// <summary>
        /// contains also pinned max and min pairs
        /// </summary>
        internal List<Tuple<int, int>> SameLayerInts { get => sameLayerInts; set => sameLayerInts = value;
        }
        List<Tuple<int, int>> upDownInts = [];

        internal List<Tuple<int, int>> UpDownInts { get => upDownInts; set => upDownInts = value;
        }

        private void CreateIntegerConstraints() {
            CreateMaxIntConstraints();
            CreateMinIntConstraints();
            CreateUpDownConstraints();
            CreateSameLayerConstraints();
        }

        private void CreateSameLayerConstraints() => SameLayerInts = CreateIntConstraintsFromStringCouples(SameLayerConstraints);

        private void CreateUpDownConstraints() => UpDownInts = CreateIntConstraintsFromStringCouples(UpDownConstraints);

        private List<Tuple<int, int>> CreateIntConstraintsFromStringCouples(Set<Tuple<Node, Node>> set) => new List<Tuple<int, int>>(from couple in set
                                                                                                                                     let t = new Tuple<int, int>(NodeIndex(couple.Item1), NodeIndex(couple.Item2))
                                                                                                                                     where t.Item1 != -1 && t.Item2 != -1
                                                                                                                                     select t);

        private void CreateMinIntConstraints() {
            minLayerInt = CreateIntConstraintsFromExtremeLayer(MinLayerOfGeomGraph);
            if (minLayerInt.Count > 0)
                minRepresentative = minLayerInt[0];
        }

        private void CreateMaxIntConstraints() {
            maxLayerInt = CreateIntConstraintsFromExtremeLayer(MaxLayerOfGeomGraph);
            if (maxLayerInt.Count > 0)
                maxRepresentative = maxLayerInt[0];
        }

        private List<int> CreateIntConstraintsFromExtremeLayer(Set<Node> setOfNodes) => new List<int>(from node in setOfNodes let index = NodeIndex(node) where index != -1 select index);
        int NodeIndex(Node node) {
            if (nodeIdToIndex.TryGetValue(node, out int index))
                return index;
            return -1;
        }
        private IEnumerable<IEdge> GetFeedbackSet() {
            gluedIntGraph = CreateGluedGraph();
            return UnglueIntPairs(CycleRemoval<IntPair>.GetFeedbackSetWithConstraints(gluedIntGraph, GluedUpDownIntConstraints));//avoiding lazy evaluation
        }

        private IEnumerable<IEdge> UnglueIntPairs(IEnumerable<IEdge> gluedEdges) {
            foreach (IEdge gluedEdge in gluedEdges)
                foreach (IEdge ungluedEdge in UnglueEdge(gluedEdge))
                    yield return ungluedEdge; 

        }

        private IEnumerable<IEdge> UnglueEdge(IEdge gluedEdge) {
            foreach (int source in UnglueNode(gluedEdge.Source))
                foreach (IntEdge edge in intGraph.OutEdges(source))
                    if (NodeToRepr(edge.Target) == gluedEdge.Target)
                        yield return edge;
        }

        private BasicGraph<IntPair> CreateGluedGraph() => new BasicGraph<IntPair>(new Set<IntPair>(from edge in intGraph.Edges select GluedIntPair(edge)), intGraph.NodeCount);


        IEnumerable<int> UnglueNode(int node) {
            if (representativeToItsLayer.TryGetValue(node, out IEnumerable<int> layer))
                return layer;
            return new int[] { node };
        }


        internal int[] GetGluedNodeCounts() {
            int[] ret = new int[nodeIdToIndex.Count];
            for (int node = 0; node < ret.Length; node++)
                ret[NodeToRepr(node)]++;
            return ret;
        }
    }
}
