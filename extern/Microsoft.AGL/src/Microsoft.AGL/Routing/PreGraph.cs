using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AGL.Core.DataStructures;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;
using Microsoft.AGL.Core.Layout;

namespace Microsoft.AGL.Routing {
    //this class contains a set of edge geometries, and set of node boundaries, ICurves, that might obstruct the edge routing 
    internal class PreGraph {
        internal List<EdgeGeometry> edgeGeometries;
        internal Set<ICurve> nodeBoundaries;
        internal Rectangle boundingBox;
       
        internal PreGraph(EdgeGeometry[] egs, Set<ICurve> nodeBoundaries) {
            edgeGeometries = new List<EdgeGeometry>(egs);
            this.nodeBoundaries = new Set<ICurve>(nodeBoundaries);
            boundingBox = Rectangle.CreateAnEmptyBox();
            foreach (ICurve curve in nodeBoundaries)
                boundingBox.Add(curve.BoundingBox);
        }

        internal PreGraph() {}

        internal void AddGraph(PreGraph a) {
            edgeGeometries.AddRange(a.edgeGeometries);
            nodeBoundaries += a.nodeBoundaries;
            boundingBox.Add(a.boundingBox);
        }

        internal void AddNodeBoundary(ICurve curve) {
            nodeBoundaries.Insert(curve);
            boundingBox.Add(curve.BoundingBox);
        }
    }
}
