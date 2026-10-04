using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered
{
    internal class EdgeComparerBySource : IComparer<LayerEdge> {
        int[] X;
        internal EdgeComparerBySource(int[] X) => this.X = X;

#if SHARPKIT //http://code.google.com/p/sharpkit/issues/detail?id=203
        //SharpKit/Colin - https://code.google.com/p/sharpkit/issues/detail?id=290
        public int Compare(LayerEdge? a, LayerEdge? b) {
#else
        int System.Collections.Generic.IComparer<LayerEdge>.Compare(LayerEdge? a, LayerEdge? b) {
#endif
            if (a is null) return b is null ? 0 : -1;
            if (b is null) return 1;
            ValidateArg.IsNotNull(a, "a");
            ValidateArg.IsNotNull(b, "b");
            int r = X[a.Source] - X[b.Source];
            if (r != 0)
                return r;

            return X[a.Target] - X[b.Target];
        }

    }
}
