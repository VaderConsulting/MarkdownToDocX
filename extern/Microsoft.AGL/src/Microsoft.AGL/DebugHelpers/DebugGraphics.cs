#if DEBUG && !SILVERLIGHT && !SHARPKIT
using System.Collections.Generic;

namespace Microsoft.AGL.DebugHelpers {
    class DebugGraphics : IDebugGraphics {
        List<DebugShape> shapes = [];
        public IList<DebugShape> Shapes => shapes;

        public void Clear() => shapes.Clear();
    }
}
#endif