using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.LargeGraphLayout {
    internal class SimpleNodeCollection(IEnumerable<Node> nodes) : IList<Node> {
        List<Node> nodes = new List<Node>(nodes);

        public IEnumerator<Node> GetEnumerator() => nodes.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Node item) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();

        public bool Contains(Node item) => throw new NotImplementedException();

        public void CopyTo(Node[] array, int arrayIndex) => nodes.CopyTo(array, arrayIndex);

        public bool Remove(Node item) => throw new NotImplementedException();

        public int Count => nodes.Count;
        public bool IsReadOnly { get; private set; }
        public int IndexOf(Node item) => throw new NotImplementedException();

        public void Insert(int index, Node item) => throw new NotImplementedException();

        public void RemoveAt(int index) => throw new NotImplementedException();

        public Node this[int index] { get => nodes[index]; set => nodes[index] = value;
        }
    }
}