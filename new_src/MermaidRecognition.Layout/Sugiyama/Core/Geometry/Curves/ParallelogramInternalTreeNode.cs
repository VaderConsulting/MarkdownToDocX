#region Using directives

using System;
using System.Collections.Generic;

#endregion

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves
{

    /// <summary>
    /// An internal node of ParallelogramNodeOverICurve hierarchy.
    /// Is used in curve intersections routines.
    /// </summary>
#if TEST_MSAGL
    
#endif
  internal class ParallelogramInternalTreeNode : ParallelogramNodeOverICurve
  {
    List<ParallelogramNodeOverICurve> children = [];

    internal ParallelogramInternalTreeNode(ICurve seg, double leafBoxesOffset):base(seg,leafBoxesOffset)
    {
    }

        /// <summary>
        /// children of the node
        /// </summary>
        /// <value></value>
        internal List<ParallelogramNodeOverICurve> Children => children;

        internal void AddChild(ParallelogramNodeOverICurve node) => children.Add(node);
    }

}
