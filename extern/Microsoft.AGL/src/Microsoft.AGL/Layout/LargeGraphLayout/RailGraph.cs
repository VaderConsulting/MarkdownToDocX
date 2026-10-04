using System.Collections;
using System.Collections.Generic;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Layout.LargeGraphLayout {
    /// <summary>
    /// class containing the visible part of the drawing
    /// </summary>
    public class RailGraph {
        /// <summary>
        /// the set of visible nodes
        /// </summary>
        public Set<Node> Nodes = [];

        /// <summary>
        /// the set of visible rails
        /// </summary>
        public Set<Rail> Rails = [];
        /// <summary>
        /// the set of visible edges
        /// </summary>
        public Set<Edge> Edges=[];
    }
}