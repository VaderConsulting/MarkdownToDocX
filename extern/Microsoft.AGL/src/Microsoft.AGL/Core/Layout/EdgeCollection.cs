using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Layout.LargeGraphLayout;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// A collection of edges.  Adding or removing edges from the collection automatically updates the related nodes.
    /// </summary>
    /// <remarks>
    /// Creates a collection for edges.
    /// </remarks>
    /// <param name="graph">The graph that each edge will be parented under.</param>
#if TEST_MSAGL

#endif
    public class EdgeCollection(GeometryGraph graph) : IEnumerable<Edge> {
        readonly IList<Edge> edges = [];
        /// <summary>
        /// the graph of the collection
        /// </summary>
        protected readonly GeometryGraph graph = graph;

        /// <summary>
        /// Adds the edge to the collection
        /// </summary>
        public virtual void Add(Edge item) {
            ValidateArg.IsNotNull(item, "item");
            item.GeometryParent = graph;
            edges.Add(item);
            AddEdgeToNodes(item);
        }

        static void AddEdgeToNodes(Edge item) {
            if (item.Source != item.Target) {
                item.Source.AddOutEdge(item);
                item.Target.AddInEdge(item);
            }
            else {
                item.Target.AddSelfEdge(item);
            }
        }

        /// <summary>
        /// Clears all of the edges.
        /// </summary>
        public virtual void Clear() {
            edges.Clear();
            foreach (Node node in graph.Nodes) {
                ((Set<Edge>) node.OutEdges).Clear();
                ((Set<Edge>) node.InEdges).Clear();
                ((Set<Edge>) node.SelfEdges).Clear();
            }
        }

        /// <summary>
        /// Returns true if the edge is found in the collection.
        /// </summary>
        /// <returns>True if the edge is found in the collection.</returns>
        public bool Contains(Edge item) {
            ValidateArg.IsNotNull(item, "item");
            return edges.Contains(item);
        }

        /// <summary>
        /// Copies the contents of the collection to the given array, starting at the given index.
        /// </summary>
        public void CopyTo(Edge[] array, int arrayIndex) => edges.CopyTo(array, arrayIndex);

        /// <summary>
        /// The number of edges in the collection.
        /// </summary>
        public virtual int Count => edges.Count;

        /// <summary>
        /// Returns false. Edge collections are never readonly.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Removes the edge from the collection.
        /// </summary>
        /// <returns>True if the edge was removed. False if the edge was not removed or the edge was not found.</returns>
        public virtual bool Remove(Edge item) {
            if (item == null || !edges.Contains(item))
            {
                return false;
            }

            DetouchEdge(item);

            return edges.Remove(item);
        }

        static void DetouchEdge(Edge item) {
            Node source = item.Source;
            Node target = item.Target;
            if (source != target) {
                source.RemoveOutEdge(item);
                target.RemoveInEdge(item);
            }
            else {
                source.RemoveSelfEdge(item);
            }
            item.GeometryParent = null;
        }

        /// <summary>
        /// Gets an enumerator for the collection.
        /// </summary>
        /// <returns>The enumerator for the colleciton.</returns>
        public virtual IEnumerator<Edge> GetEnumerator() => edges.GetEnumerator();

        /// <summary>
        /// Gets an enumerator for the collection.
        /// </summary>
        /// <returns>The enumerator for the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)edges).GetEnumerator();
    }
}
