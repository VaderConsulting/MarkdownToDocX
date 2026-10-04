#region Using directives



#endregion

using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered.Layering
{
    /// <summary>
    /// Layering the DAG by longest path
    /// </summary>
    internal class LongestPathLayering : LayerCalculator {

        BasicGraph<IntEdge> graph;

        public int[] GetLayers() {
            //sort the vertices in topological order
            int[] topoOrder = IntEdge.GetOrder(graph);
            int[] layering = new int[graph.NodeCount];

            //going backward from leaves
            int k = graph.NodeCount;
            while (k-- > 0) {
                int v = topoOrder[k];
                foreach (IntEdge e in graph.InEdges(v)) {
                    int u = e.Source;
                    int l = layering[v] + e.Separation;
                    if (layering[u] < l)
                    {
                        layering[u] = l;
                    }
                }
            }
            return layering;

        }

        internal LongestPathLayering(BasicGraph<IntEdge> graph) => this.graph = graph;
    }
}
