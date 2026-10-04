using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.ConeSpanner {
    internal abstract class SweepEvent {
        internal abstract Point Site { get; }

        /// <summary/>
        public override string ToString() {
            string typeString = GetType().ToString();
            int lastDotLoc = typeString.LastIndexOf('.');
            if (lastDotLoc >= 0) {
                typeString = typeString.Substring(lastDotLoc + 1);
            }
            return string.Format("{0} {1}", typeString, Site);
        }
    }
}
