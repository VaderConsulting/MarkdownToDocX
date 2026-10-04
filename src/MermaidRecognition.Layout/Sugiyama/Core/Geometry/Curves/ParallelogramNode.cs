#region Using directives



#endregion

using System;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves {
    /// <summary>
    /// Represents a node containing a parallelogram.
    /// Is used in curve intersections routines.
    /// </summary>
#if TEST_MSAGL
    
#endif
    public abstract class ParallelogramNode {
        Parallelogram parallelogram;
        /// <summary>
        /// gets or sets the parallelogram of the node
        /// </summary>
        public Parallelogram Parallelogram { get => parallelogram; set => parallelogram = value;
        }
    }
}
