using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Layout.Layered {
    internal class RecoveryLayerCalculator(LayerArrays recoveredLayerArrays) : LayerCalculator {
        LayerArrays layers = recoveredLayerArrays;

        public int[] GetLayers() => layers.Y;
    }
}