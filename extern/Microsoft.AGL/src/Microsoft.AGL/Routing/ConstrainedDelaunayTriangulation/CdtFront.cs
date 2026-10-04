using Microsoft.AGL.Core.DataStructures.RBTree;

namespace Microsoft.AGL.Routing.ConstrainedDelaunayTriangulation {
    internal class CdtFront
    {
        readonly RbTree<CdtSite> front = new RbTree<CdtSite>((a, b) => a.Point.X.CompareTo(b.Point.X));
    }
}
