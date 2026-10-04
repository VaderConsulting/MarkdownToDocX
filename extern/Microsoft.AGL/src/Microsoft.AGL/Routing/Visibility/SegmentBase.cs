using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Visibility {
    internal abstract class SegmentBase {
        internal abstract Point Start { get; }
        internal abstract Point End { get; }
        internal Point Direction => End - Start;
#if DEBUG
        public override string ToString() => Start + " " + End;
#endif
    }
}