using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Visibility {
    internal abstract class SegmentBase {
        internal abstract Point Start { get; }
        internal abstract Point End { get; }
        internal Point Direction => End - Start;
#if DEBUG
        public override string ToString() => Start + " " + End;
#endif
    }
}