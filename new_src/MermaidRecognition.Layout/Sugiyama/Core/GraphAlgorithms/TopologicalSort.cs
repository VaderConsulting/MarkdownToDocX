using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms{
    /// <summary>
    /// Implements the topological sort
    /// </summary>
    public static class TopologicalSort{
#if NET4
        /// <summary>
        /// Do a topological sort of a list of int edge tuples
        /// </summary>
        /// <param name="numberOfVertices">number of vertices</param>
        /// <param name="edges">edge pairs</param>
        /// <returns>ordered node indexes</returns>
        public static int[] GetOrder(int numberOfVertices, IEnumerable<System.Tuple<int,int>> edges)
        {
            BasicGraph<IntPair> dag = new BasicGraph<IntPair>(from e in edges select new IntPair(e.Item1, e.Item2), numberOfVertices);
            return GetOrder(dag);
        }
#endif

        /// <summary>
        /// The function returns an array arr such that
        /// arr is a permutation of the graph vertices,
        /// and for any edge e in graph if e.Source=arr[i]
        /// e.Target=arr[j], then i is less than j
        /// </summary>
        /// <param name="graph"></param>
        /// <returns></returns>
        internal static int[] GetOrder<TEdge>(BasicGraph<TEdge> graph) where TEdge : IEdge{
            bool[] visited = new bool[graph.NodeCount];

            //no recursion! So we have to organize a stack
            Stack<int> sv = new Stack<int>();
            Stack<IEnumerator<int>> se = new Stack<IEnumerator<int>>();

            List<int> order = new List<int>();

            IEnumerator<int> en;
            for (int u = 0; u < graph.NodeCount; u++){
                if (visited[u])
                    continue;

                int cu = u;
                visited[cu] = true;
                en = graph.OutEdges(u).Select(e => e.Target).GetEnumerator();

                do{
                    while (en.MoveNext()){
                        int v = en.Current;
                        if (!visited[v]){
                            visited[v] = true;
                            sv.Push(cu);
                            se.Push(en);
                            cu = v;
                            en = graph.OutEdges(cu).Select(e => e.Target).GetEnumerator();
                        }
                    }
                    order.Add(cu);


                    if (sv.Count > 0){
                        en = se.Pop();
                        cu = sv.Pop();
                    }
                    else
                        break;
                } while (true);
            }
            order.Reverse();
            return order.ToArray();
        }

        /// <summary>
        /// The function returns an array arr such that
        /// arr is a permutation of edge  vertices,
        /// and for any edge e from the list, if e.Source=arr[i]
        /// e.Target=arr[j], then i is less than j
        /// </summary>
        /// <returns></returns>
        internal static int[] GetOrderOnEdges<TEdge>(IEnumerable<TEdge> edges) where TEdge : IEdge {
            Dictionary<int, bool> visited = new Dictionary<int,bool>();
            Dictionary<int, List<int>> graph = new Dictionary<int, List<int>>();
            foreach (TEdge e in edges) {
                visited[e.Source]=visited[e.Target]=false;
                int x =e.Source;
                if (!graph.TryGetValue(x, out List<int>? list))
                {
                    graph[x] = list = [];
                }
                list.Add(e.Target);
            }
            //organize a couple of stacks to avoid the recursion
            Stack<int> sv = new Stack<int>();
            Stack<IEnumerator<int>> se = new Stack<IEnumerator<int>>();

            List<int> order = new List<int>();

            IEnumerator<int> en;
            foreach (int u in visited.Keys.ToArray()) {
                if (visited[u])
                    continue;

                int cu = u;

                visited[cu] = true;
                if (!graph.TryGetValue(u, out List<int>? glist)) continue;
                if (glist == null) continue;
                en = glist.GetEnumerator();
                do {
                    while (en.MoveNext()) {
                        int v = en.Current;
                        if (!visited[v]) {
                            visited[v] = true;
                            if (!graph.TryGetValue(v, out List<int>? list))
                            {
                                order.Add(v);//it is a leaf
                                continue;
                            }
                            sv.Push(cu);
                            se.Push(en);
                            cu = v;
                            en = list.GetEnumerator();
                        }
                    }
                    order.Add(cu);

                    if (sv.Count > 0) {
                        en = se.Pop();
                        cu = sv.Pop();
                    } else
                        break;
                } while (true);
            }
            order.Reverse();
            return order.ToArray();
        }
    }
}