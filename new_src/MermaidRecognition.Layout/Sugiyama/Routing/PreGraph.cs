using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MermaidRecognition.Layout.Sugiyama.Core.DataStructures;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;
using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Routing {
    //this class contains a set of edge geometries, and set of node boundaries, ICurves, that might obstruct the edge routing 
    internal class PreGraph {
        internal List<EdgeGeometry> edgeGeometries;
        internal Set<ICurve> nodeBoundaries;
        internal Rectangle boundingBox;
       
        internal PreGraph(EdgeGeometry[] egs, Set<ICurve> nodeBoundaries) {
            edgeGeometries = new List<EdgeGeometry>(egs);
            this.nodeBoundaries = new Set<ICurve>(nodeBoundaries);
            boundingBox = Rectangle.CreateAnEmptyBox();
            foreach (var curve in nodeBoundaries)
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
