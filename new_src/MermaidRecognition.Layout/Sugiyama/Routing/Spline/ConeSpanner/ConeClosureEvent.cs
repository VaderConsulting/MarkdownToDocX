using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    /// <summary>
    /// this event caused by the intersection of a ObstacleSideSegment and the other cone side of the same cone
    /// when this event happens the cone has to be removed
    /// </summary>
    internal class ConeClosureEvent:SweepEvent {
        Cone coneToClose;

        internal Cone ConeToClose => coneToClose;
        Point site;

        internal override Point Site => site;

        internal ConeClosureEvent(Point site, Cone cone) {
            this.site = site;
            coneToClose = cone;
        }

        public override string ToString() => "ConeClosureEvent " + site;
    }
}
