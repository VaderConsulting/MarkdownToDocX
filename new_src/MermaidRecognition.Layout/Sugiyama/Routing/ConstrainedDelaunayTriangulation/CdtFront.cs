using MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree;

namespace MermaidRecognition.Layout.Sugiyama.Routing.ConstrainedDelaunayTriangulation {
    internal class CdtFront
    {
        readonly RbTree<CdtSite> front = new RbTree<CdtSite>((a, b) => a.Point.X.CompareTo(b.Point.X));
    }
}
