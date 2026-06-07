#if DEBUG && !SILVERLIGHT && !SHARPKIT
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
    class DebugGraphics : IDebugGraphics {
        List<DebugShape> shapes = new List<DebugShape>();
        public IList<DebugShape> Shapes {
            get { return shapes; }
        }

        public void Clear() {
            shapes.Clear();
        }
    }
}
#endif