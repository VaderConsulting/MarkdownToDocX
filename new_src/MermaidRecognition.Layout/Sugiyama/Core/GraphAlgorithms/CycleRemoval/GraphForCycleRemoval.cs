using System.Collections.Generic;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;

namespace MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms.CycleRemoval
{

    internal class GraphForCycleRemoval {
        /// <summary>
        /// this dictionary contains only buckets with nodes which are sources in the graph of constrained edges
        /// </summary>
        SortedDictionary<int, Set<int>> deltaDegreeBucketsForSourcesInConstrainedSubgraph = [];
        Dictionary<int, NodeInfo> nodeInfoDictionary = [];
        Set<int> sources=[];
        Set<int> sinks=[];
        Set<IntPair> edgesToKeep = [];

        internal void AddEdge(IntPair edge) {
            edgesToKeep.Insert(edge);
            int source = edge.First; int target = edge.Second;
            GetOrCreateNodeInfo(source).AddOutEdge(target);
            GetOrCreateNodeInfo(target).AddInEdge(source);
        }


         NodeInfo GetOrCreateNodeInfo(int node) {
            if (!nodeInfoDictionary.TryGetValue(node, out NodeInfo? nodeInfo))
            {
                nodeInfo = new NodeInfo();
                nodeInfoDictionary[node] = nodeInfo;
            }
            return nodeInfo;
        }

        internal void AddConstraintEdge(IntPair intPair) {
            int source = intPair.First; int target = intPair.Second;
            GetOrCreateNodeInfo(source).AddOutConstrainedEdge(target);
            GetOrCreateNodeInfo(target).AddInConstrainedEdge(source);
        }

        internal bool IsEmpty() => nodeInfoDictionary.Count == 0;

        internal void RemoveNode(int u) {
            sources.Remove(u);
            sinks.Remove(u);
            RemoveNodeFromItsBucket(u);
            NodeInfo uNodeInfo=nodeInfoDictionary[u];
            Set<int> allNbs = new Set<int>(uNodeInfo.AllNeighbors);
            foreach(int v in allNbs)
                RemoveNodeFromItsBucket(v);

            DisconnectNodeFromGraph(u, uNodeInfo);

            foreach (int v in allNbs)
                AddNodeToBucketsSourcesAndSinks(v, nodeInfoDictionary[v]);
  

        }

         void DisconnectNodeFromGraph(int u, NodeInfo uNodeInfo) {
            foreach (int v in uNodeInfo.OutEdges)
                nodeInfoDictionary[v].RemoveInEdge(u);

            foreach (int v in uNodeInfo.OutConstrainedEdges)
                nodeInfoDictionary[v].RemoveInConstrainedEdge(u);

            foreach (int v in uNodeInfo.InEdges)
                nodeInfoDictionary[v].RemoveOutEdge(u);

            foreach (int v in uNodeInfo.InConstrainedEdges)
                nodeInfoDictionary[v].RemoveOutConstrainedEdge(u);

            nodeInfoDictionary.Remove(u);
        }

        

         void RemoveNodeFromItsBucket(int v) {
            int delta = DeltaDegree(v);
            if (deltaDegreeBucketsForSourcesInConstrainedSubgraph.TryGetValue(delta, out Set<int>? bucket))
            {
                bucket.Remove(v);
                if (bucket.Count == 0)
                    deltaDegreeBucketsForSourcesInConstrainedSubgraph.Remove(delta);
            }
        }

         int DeltaDegree(int v) {
            int delta = nodeInfoDictionary[v].DeltaDegree;
            return delta;
        }



        internal bool TryFindVertexWithNoIncomingConstrainedEdgeAndMaximumOutDegreeMinusInDedree(out int u) {
            SortedDictionary<int, Set<int>>.Enumerator enumerator = deltaDegreeBucketsForSourcesInConstrainedSubgraph.GetEnumerator();
            if (enumerator.MoveNext()) {
                Set<int> bucketSet = enumerator.Current.Value;
                System.Diagnostics.Debug.Assert(bucketSet.Count > 0);
                IEnumerator<int> nodeEnumerator = bucketSet.GetEnumerator();
                nodeEnumerator.MoveNext();
                u = nodeEnumerator.Current;
                return true;
            }
            u = -1;
            return false;
        }

        internal bool TryGetSource(out int u) {
            IEnumerator<int> enumerator = sources.GetEnumerator();
            if (enumerator.MoveNext()) {
                u = enumerator.Current;
                return true;
            }
            u = -1;
            return false;
        }

        internal bool TryGetSink(out int u) {
            IEnumerator<int> enumerator = sinks.GetEnumerator();
            if (enumerator.MoveNext()) {
                u = enumerator.Current;
                return true;
            }
            u = -1;
            return false;
        }

        internal IEnumerable<IntPair> GetOriginalIntPairs() => edgesToKeep;

        internal void Initialize() {
            foreach (KeyValuePair<int, NodeInfo> p in nodeInfoDictionary) 
                AddNodeToBucketsSourcesAndSinks(p.Key, p.Value);
        }

         void AddNodeToBucketsSourcesAndSinks(int v, NodeInfo nodeInfo) {
            if (nodeInfo.InDegree == 0)
                sources.Insert(v);
            else if (nodeInfo.OutDegree == 0)
                sinks.Insert(v);
            else if (nodeInfo.InDegreeOfConstrainedEdges == 0)
                GetOrCreateBucket(nodeInfo.DeltaDegree).Insert(v);
        }

         Set<int> GetOrCreateBucket(int delta) {
            if (deltaDegreeBucketsForSourcesInConstrainedSubgraph.TryGetValue(delta, out Set<int>? ret))
                return ret;
            return deltaDegreeBucketsForSourcesInConstrainedSubgraph[delta] = [];
        }
    }
}
