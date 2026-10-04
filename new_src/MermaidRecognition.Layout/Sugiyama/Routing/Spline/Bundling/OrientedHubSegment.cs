using MermaidRecognition.Layout.Sugiyama.Core.Geometry;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    internal class OrientedHubSegment {
        internal ICurve Segment;
        internal bool Reversed;
        internal int Index;
        internal BundleBase BundleBase;

        internal OrientedHubSegment(ICurve seg, bool reversed, int index, BundleBase bundleBase) {
            Segment = seg;
            Reversed = reversed;
            Index = index;
            BundleBase = bundleBase;
        }

        internal Point this[double t] { get { return Reversed ? Segment[Segment.ParEnd - t] : Segment[t]; } }

        internal Point Start => !Reversed ? Segment.Start : Segment.End;

        internal Point End => Reversed ? Segment.Start : Segment.End;

        internal OrientedHubSegment Other { get; set; }
    }
}