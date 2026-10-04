using System;
using System.Collections;
using System.Collections.Generic;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    internal class SimpleNodeCollection : IList<Node> {
        List<Node> nodes;

        public SimpleNodeCollection(IEnumerable<Node> nodes) => this.nodes = new List<Node>(nodes);

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