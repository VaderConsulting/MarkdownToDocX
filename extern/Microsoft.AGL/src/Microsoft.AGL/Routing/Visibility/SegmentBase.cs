using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Visibility {
    internal abstract class SegmentBase {
        abstract internal Point Start { get; }
        abstract internal Point End { get; }
        internal Point Direction { get { return End - Start; } }
#if DEBUG
        public override string ToString() {
            return Start + " " + End;
        }
#endif
    }
}