using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout {
    internal class SimpleIntEdge : IEdge
    {
        public int Source { get; set; }
        public int Target { get; set; }
    }
}