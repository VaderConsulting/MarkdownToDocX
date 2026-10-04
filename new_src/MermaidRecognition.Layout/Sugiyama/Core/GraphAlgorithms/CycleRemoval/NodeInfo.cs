using System.Collections.Generic;

using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;

namespace MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms.CycleRemoval
{
    internal class NodeInfo {
        Set<int> outEdges = [];

        internal Set<int> OutEdges => outEdges;
        Set<int> inEdges = [];

        internal Set<int> InEdges => inEdges;
        Set<int> outConstrainedEdges = [];

        internal Set<int> OutConstrainedEdges => outConstrainedEdges;

        Set<int> inConstrainedEdges = [];

        public Set<int> InConstrainedEdges => inConstrainedEdges;
        /// <summary>
        /// it is the out degree without the in degree
        /// </summary>
        internal int DeltaDegree => InDegree - OutDegree;
        internal void AddOutEdge(int v) => outEdges.Insert(v);
        internal void RemoveOutEdge(int v) => outEdges.Remove(v);

        internal void AddInEdge(int v) => inEdges.Insert(v);
        internal void RemoveInEdge(int v) => inEdges.Remove(v);
        internal void AddOutConstrainedEdge(int v) => outConstrainedEdges.Insert(v);
        internal void RemoveOutConstrainedEdge(int v) => outConstrainedEdges.Remove(v);

        internal void AddInConstrainedEdge(int v) => inConstrainedEdges.Insert(v);
        internal void RemoveInConstrainedEdge(int v) => inConstrainedEdges.Remove(v);

        internal int OutDegree => outEdges.Count + outConstrainedEdges.Count;
        internal int InDegreeOfConstrainedEdges => inConstrainedEdges.Count;
        internal int InDegree => inEdges.Count + inConstrainedEdges.Count;


        /// <summary>
        /// including constrained neighbors
        /// </summary>
        internal IEnumerable<int> AllNeighbors {
            get {
                foreach (int v in OutConstrainedEdges)
                    yield return v;
                foreach (int v in InConstrainedEdges)
                    yield return v;
                foreach (int v in OutEdges)
                    yield return v;
                foreach (int v in InEdges)
                    yield return v;
            }
        }
    }
}
