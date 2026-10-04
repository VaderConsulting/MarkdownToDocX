using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Incremental {
    /// <summary>
    /// An edge is a pair of nodes and an ideal length required between them
    /// </summary>
    internal class FiEdge(Edge mEdge) : IEdge {
        internal Edge mEdge = mEdge;
        public FiNode source = (FiNode)mEdge.Source.AlgorithmData;
        public FiNode target = (FiNode)mEdge.Target.AlgorithmData;
        #region IEdge Members

        public int Source
        {
            get => source.index;
            set { }
        }

        public int Target
        {
            get => target.index;
            set { }
        }

        #endregion

        internal Point vector() => source.mNode.Center - target.mNode.Center;
    }
}