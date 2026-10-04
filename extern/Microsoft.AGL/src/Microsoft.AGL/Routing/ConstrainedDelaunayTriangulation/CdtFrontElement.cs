using System.Diagnostics;

namespace Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation {
    internal class CdtFrontElement {
        //The LeftSite should coincide with the leftmost end of the Edge, and the edge should not be vertical

        internal CdtSite LeftSite;
        internal CdtEdge Edge;
        internal CdtSite RightSite;

        internal double X => LeftSite.Point.X;

        internal CdtFrontElement(CdtSite leftSite, CdtEdge edge) {
            Debug.Assert(edge.upperSite.Point.X != edge.lowerSite.Point.X &&
                         edge.upperSite.Point.X < edge.lowerSite.Point.X && leftSite == edge.upperSite ||
                         edge.upperSite.Point.X > edge.lowerSite.Point.X && leftSite == edge.lowerSite);
            RightSite = edge.upperSite == leftSite ? edge.lowerSite : edge.upperSite;
            LeftSite = leftSite;
            Edge = edge;
        }
    }
}