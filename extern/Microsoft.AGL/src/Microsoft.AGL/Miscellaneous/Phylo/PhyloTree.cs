using System.Collections.Generic;

using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Miscellaneous.Phylo
{
    /// <summary>
    /// Implements a phylogenetic tree
    /// </summary>
    public class PhyloTree : GeometryGraph {
        Node root;

        //used in the other solution
        internal Node Root => root != null ? root : root = GetRoot();

        private Node GetRoot() {
            Node ret = null;
            foreach (Node n in Nodes) {
                ret = n; break;
            }

            Node oldVal;
            do {
                oldVal = ret;
                foreach (Edge e in ret.InEdges)
                {
                    ret = e.Source;
                }
            } while (oldVal != ret);
     
            return ret;
        }
        /// <summary>
        /// the leaves of the tree
        /// </summary>
        public IEnumerable<Node> Leaves {
            get {
                foreach (Node node in Nodes)
                {
                    if (NodeIsALeaf(node))
                    {
                        yield return node;
                    }
                }
            }
        }

        private static bool NodeIsALeaf(Node node) => !node.OutEdges.GetEnumerator().MoveNext();
    }
}
