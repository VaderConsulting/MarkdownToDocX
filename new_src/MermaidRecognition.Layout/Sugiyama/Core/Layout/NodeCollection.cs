using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout
{
    /// <summary>
    /// A collection of nodes.  Adding or removing nodes from the collection automatically updates the graph.
    /// </summary>
    /// <remarks>
    /// Creates a collection for nodes.
    /// </remarks>
    /// <param name="graph">The graph that each node will be parented under.</param>
#if TEST_MSAGL

#endif
    internal class NodeCollection(GeometryGraph graph) : IList<Node>
    {
        internal readonly List<Node> nodes = [];

         GeometryGraph graph = graph;

        /// <summary>
        /// Adds the node to the collection, but does not add the node edges to the graph
        /// </summary>
        public void Add(Node item)
        {
            ValidateArg.IsNotNull(item, "node");
            nodes.Add(item);
            item.GeometryParent = graph;
        }

        /// <summary>
        /// Clears all of the nodes.
        /// </summary>
        public void Clear() => nodes.Clear();

        /// <summary>
        /// Returns true if the node is found in the collection.
        /// </summary>
        /// <returns>True if the node is found in the collection.</returns>
        public bool Contains(Node item) => nodes.Contains(item);

        /// <summary>
        /// Copies the contents of the collection to the given array, starting at the given index.
        /// </summary>
        public void CopyTo(Node[] array, int arrayIndex) => nodes.CopyTo(array, arrayIndex);

        /// <summary>
        /// The number of nodes in the collection.
        /// </summary>
        public int Count => nodes.Count;

        /// <summary>
        /// Returns false. Node collections are never readonly.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Removes the node from the collection.
        /// </summary>
        /// <returns>True if the node was removed. False if the node was not removed or the node was not found.</returns>
        public bool Remove(Node item)
        {
            if (item == null)
            {
                return false;
            }

            DetouchNode(item);
            return nodes.Remove(item);
        }

        void DetouchNode(Node item) {
            Edge[] nodeEdges = item.Edges.ToArray(); //cannot use lazy evaluation here
            foreach (Edge? edge in nodeEdges)
            {
                graph.Edges.Remove(edge);
            }

            item.GeometryParent = null;
        }

        /// <summary>
        /// Gets an enumerator for the collection.
        /// </summary>
        /// <returns>The enumerator for the colleciton.</returns>
        public IEnumerator<Node> GetEnumerator() => nodes.GetEnumerator();

        /// <summary>
        /// Gets an enumerator for the collection.
        /// </summary>
        /// <returns>The enumerator for the collection.</returns>
        IEnumerator IEnumerable.GetEnumerator() => nodes.GetEnumerator();

        /// <summary>
        /// Determines the index of a specific item in the <see cref="T:System.Collections.Generic.IList`1"/>.
        /// </summary>
        /// <returns>
        /// The index of <paramref name="item"/> if found in the list; otherwise, -1.
        /// </returns>
        /// <param name="item">The object to locate in the <see cref="T:System.Collections.Generic.IList`1"/>.</param>
        public int IndexOf(Node item) {
            ValidateArg.IsNotNull(item, "item");
            return nodes.IndexOf(item);
        }

        /// <summary>
        /// Inserts an item to the <see cref="T:System.Collections.Generic.IList`1"/> at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param><param name="item">The object to insert into the <see cref="T:System.Collections.Generic.IList`1"/>.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void Insert(int index, Node item) {
            ValidateArg.IsNotNull(item,"item");
            nodes.Insert(index, item);
        }

        /// <summary>
        /// Removes the <see cref="T:System.Collections.Generic.IList`1"/> item at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public void RemoveAt(int index) {
            Node node = nodes[index];
            DetouchNode(node);
            nodes.RemoveAt(index);
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <returns>
        /// The element at the specified index.
        /// </returns>
        /// <param name="index">The zero-based index of the element to get or set.</param><exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index"/> is not a valid index in the <see cref="T:System.Collections.Generic.IList`1"/>.</exception><exception cref="T:System.NotSupportedException">The property is set and the <see cref="T:System.Collections.Generic.IList`1"/> is read-only.</exception>
        public Node this[int index]
        {
            get => nodes[index];
            set
            {
                ValidateArg.IsNotNull(value, "value");
                Node node = nodes[index];
                if (node != value)
                {
                    DetouchNode(node);
                    value.GeometryParent = graph;
                    nodes[index] = value;
                }
            }
        }
    }
}
