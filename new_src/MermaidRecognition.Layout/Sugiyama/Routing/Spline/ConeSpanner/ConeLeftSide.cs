using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal class ConeLeftSide:ConeSide {
        internal ConeLeftSide(Cone cone) => Cone = cone;

        internal override Point Start => Cone.Apex;

        internal override Point Direction => Cone.LeftSideDirection;
#if TEST_MSAGL
        public override string ToString() => "ConeLeftSide " + Start + " " + Direction;
#endif
    }
}
