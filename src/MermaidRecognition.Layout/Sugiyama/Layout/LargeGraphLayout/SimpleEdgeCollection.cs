using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.LargeGraphLayout {
    internal class SimpleEdgeCollection(IEnumerable<Edge> collectionEdges) : EdgeCollection(null) {
        readonly List<Edge> edges = collectionEdges as List<Edge> ??
                [.. collectionEdges];

        public override IEnumerator<Edge> GetEnumerator() => edges.GetEnumerator();

        public override int Count => edges.Count;

        public override void Add(Edge item) => throw new NotImplementedException();

        public override void Clear() => throw new NotImplementedException();

        public override bool Remove(Edge item) => throw new NotImplementedException();
    }
}