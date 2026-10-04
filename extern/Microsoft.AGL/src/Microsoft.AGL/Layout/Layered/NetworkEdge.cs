using System;
using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Layout.Layered {
    /// <summary>
    /// Differs from IntEdge in containing a flag indicating belonging to the tree
    /// and containing the cut value
    /// </summary>
    internal class NetworkEdge : IntEdge {

        internal const int Infinity = int.MaxValue;
        internal NetworkEdge(IntEdge e)
            : base(e.Source, e.Target) {
            Weight = e.Weight;
            Separation = e.Separation;
        }
        internal bool inTree;
        int cut = Infinity;

        internal int Cut { get => cut; set => cut = value;
        }
    }

}
