using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Layout.Layered {
    internal class RecoveryLayerCalculator : LayerCalculator {
        LayerArrays layers;

        public RecoveryLayerCalculator(LayerArrays recoveredLayerArrays) => layers = recoveredLayerArrays;
        public int[] GetLayers() => layers.Y;
    }
}