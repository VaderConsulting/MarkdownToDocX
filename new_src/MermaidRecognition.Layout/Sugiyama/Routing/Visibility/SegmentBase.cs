using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    internal abstract class SegmentBase {
        abstract internal Point Start { get; }
        abstract internal Point End { get; }
        internal Point Direction => End - Start;
#if DEBUG
        public override string ToString() => Start + " " + End;
#endif
    }
}