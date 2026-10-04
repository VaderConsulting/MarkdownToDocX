using System.Collections.Generic;
using System.Linq;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Layout.Layered {
    /// <summary>
    /// This class holds assorted data associated with the graph under layout: list of anchors, 
    /// edges sorted by their sources,targets etc
    /// </summary>
#if TEST_MSAGL
    public 
#else
    internal
#endif
        class Database {

        /// <summary>
        /// maps middles of multiple strings to their buckets
        /// </summary>
        Set<int> multipleMiddles = [];
#if TEST_MSAGL
        /// <summary>
        /// The layer to visualize. Is set to zero after each display
        /// </summary>
        public int[] nodesToShow; //set it to the layer index to display. The 
        /// <summary>
        /// 
        /// </summary>
        public
#else
        internal
#endif
 Set<int> MultipleMiddles => multipleMiddles;

        /// <summary>
        /// This table keeps multi edges
        /// </summary>
        Dictionary<IntPair, List<IntEdge>> multiedges = [];

        internal IEnumerable<IntEdge> AllIntEdges {
            get {
                foreach (List<IntEdge> l in Multiedges.Values)
                    foreach (IntEdge e in l)
                        yield return e;
            }
        }



        internal Anchor[] anchors;

        /// <summary>
        /// Anchors of the nodes
        /// </summary>

#if TEST_MSAGL
        public
#else
        internal
#endif

Anchor[] Anchors { get => anchors; set => anchors = value;
        }

        internal void AddFeedbackSet(IEnumerable<IEdge> edges) {
            foreach (IEdge e in edges) {
                IntPair ip = new IntPair(e.Source, e.Target);
                IntPair ipr = new IntPair(e.Target, e.Source);

                //we shuffle reversed edges into the other multiedge
                List<IntEdge> listToShuffle = multiedges[ip];
                foreach (IntEdge er in listToShuffle)
                    er.Revert();

                if (multiedges.ContainsKey(ipr))
                    multiedges[ipr].AddRange(listToShuffle);
                else
                    multiedges[ipr] = listToShuffle;

                multiedges.Remove(ip);
            }
        }

        internal IEnumerable<List<IntEdge>> RegularMultiedges {
            get {
                foreach (KeyValuePair<IntPair, List<IntEdge>>
                        kv in Multiedges)
                    if (kv.Key.x != kv.Key.y)
                        yield return kv.Value;
            }
        }
#if TEST_MSAGL
        /// <summary>
        /// 
        /// </summary>
        public
#else
        internal
#endif
Dictionary<IntPair, List<IntEdge>> Multiedges => multiedges;


        internal List<IntEdge> GetMultiedge(int source, int target) => GetMultiedge(new IntPair(source, target));


        internal List<IntEdge> GetMultiedge(IntPair ip) {
            if (multiedges.ContainsKey(ip))
                return multiedges[ip];

            return [];
        }


        internal void RegisterOriginalEdgeInMultiedges(IntEdge edge) {
            IntPair ip = new IntPair(edge.Source, edge.Target);
            List<IntEdge> o;
            if (multiedges.ContainsKey(ip) == false)
                multiedges[ip] = o = [];
            else
                o = multiedges[ip];

            o.Add(edge);
        }

        internal IEnumerable<IntEdge> SkeletonEdges() => from kv in Multiedges where kv.Key.x != kv.Key.y select kv.Value[0];
    }
}
