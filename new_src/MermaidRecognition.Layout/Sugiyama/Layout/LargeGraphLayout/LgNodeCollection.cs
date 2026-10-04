using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    internal class LgNodeCollection : IList<Node> {
        readonly Func<IEnumerable<Node>> funcOfNodes;

        public LgNodeCollection(Func<IEnumerable<LgNodeInfo>> funcOfLgNodes) => funcOfNodes = () => funcOfLgNodes().Select(n => n.GeometryNode);

        public IEnumerator<Node> GetEnumerator() => funcOfNodes().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add(Node item) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();

        public bool Contains(Node item) => throw new NotImplementedException();

        public void CopyTo(Node[] array, int arrayIndex) => throw new NotImplementedException();

        public bool Remove(Node item) => throw new NotImplementedException();

        public int Count => funcOfNodes().Count();
        public bool IsReadOnly { get;  set; }
        public int IndexOf(Node item) => throw new NotImplementedException();

        public void Insert(int index, Node item) => throw new NotImplementedException();

        public void RemoveAt(int index) => throw new NotImplementedException();

        public Node this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException();
        }
    }
}