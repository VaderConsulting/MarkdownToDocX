using System;
using System.Collections.Generic;

using Microsoft.AGL.Core.DataStructures;

namespace Microsoft.AGL.Layout.Layered
{
    internal class LayerInfo {
        /// <summary>
        /// constrained on the level of neighBlocks
        /// </summary>
        internal Set<Tuple<int, int>> leftRight = [];
        internal Set<Tuple<int, int>> flatEdges = [];
        internal Dictionary<int, List<int>> neigBlocks = [];
        internal Dictionary<int, int> constrainedFromAbove = [];
        internal Dictionary<int, int> constrainedFromBelow = [];
        internal Dictionary<int, int> nodeToBlockRoot = [];
        /// <summary>
        /// if the block contains a fixed node v,  it can be only one because of the monotone paths feature,
        /// then blockToFixedNodeOfBlock[block]=v
        /// /// </summary>
        
        internal Dictionary<int, int> blockRootToVertConstrainedNodeOfBlock = [];
    }
}
