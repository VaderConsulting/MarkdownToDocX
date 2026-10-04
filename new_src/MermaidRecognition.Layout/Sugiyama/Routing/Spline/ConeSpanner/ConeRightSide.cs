using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal class ConeRightSide:ConeSide {
        internal ConeRightSide(Cone cone) => Cone = cone;

        internal override Point Start => Cone.Apex;

        internal override Point Direction => Cone.RightSideDirection;

        public override string ToString() => "ConeRightSide " + Start + " " + Direction;
    }
}
