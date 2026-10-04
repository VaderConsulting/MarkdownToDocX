using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;

using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Routing.Rectilinear.Nudging {
    /// <summary>
    /// represents the path for an EdgeGeometry 
    /// </summary>
    internal class Path {
        /// <summary>
        /// the corresponding edge geometry
        /// </summary>
        internal EdgeGeometry EdgeGeometry { get; set; }
        /// <summary>
        /// the path points
        /// </summary>
        internal IEnumerable<Point> PathPoints { get; set; }

        internal double Width => EdgeGeometry.LineWidth;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="edgeGeometry"></param>
        internal Path(EdgeGeometry edgeGeometry) => EdgeGeometry = edgeGeometry;


        internal Point End => LastEdge.Target;

        internal Point Start => FirstEdge.Source;


        internal IEnumerable<PathEdge> PathEdges {
            get {
                for (PathEdge e = FirstEdge; e != null; e = e.Next)
                {
                    yield return e;
                }
            }
        }

        internal PathEdge FirstEdge { get; set; }

        internal PathEdge LastEdge { get; set; }

        internal void SetIsFixedByFollowingParallelEdges() {
//            SetIsFixedByFollowingParallelEdgesOnEnumeration(PathEdges);
//            SetIsFixedByFollowingParallelEdgesOnEnumeration(PathEdgesInReverseOrder());
            
            //            if(GetFirstPathEdge().IsFixed)
//                foreach(var edge in ParallelEdgesStartingFromFirst())
//                    edge.IsFixed = true;
//            if (GetLastPathEdge().IsFixed)
//                foreach (var edge in ParallelEdgesStartingFromLast())
//                    edge.IsFixed = true;

        }

        internal void AddEdge(PathEdge edge) {
            edge.Path = this;
            Debug.Assert(edge.Source == LastEdge.Target);
            LastEdge.Next = edge;
            edge.Prev = LastEdge;
            LastEdge = edge;
        }

        internal void SetFirstEdge(PathEdge edge) {
            LastEdge = FirstEdge = edge;
            edge.Path = this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}->{1}", EdgeGeometry.SourcePort.Location, EdgeGeometry.TargetPort.Location);
    }
}