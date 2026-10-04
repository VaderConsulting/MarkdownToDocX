using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    internal class PointPairOrder {
        //order for node u of edge u->v
        internal List<Metroline> Metrolines = [];
        internal bool orderFixed;
        internal Dictionary<Metroline, int> LineIndexInOrder;

        internal int Count => Metrolines.Count;

        internal void Add(Metroline metroline) => Metrolines.Add(metroline);
    }
}