using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Core.Layout {
    internal class SimpleIntEdge : IEdge
    {
        public int Source { get; set; }
        public int Target { get; set; }
    }
}