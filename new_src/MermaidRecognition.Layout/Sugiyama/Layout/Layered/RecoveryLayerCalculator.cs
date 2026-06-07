using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered {
    internal class RecoveryLayerCalculator : LayerCalculator {
        LayerArrays layers;

        public RecoveryLayerCalculator(LayerArrays recoveredLayerArrays) {
            layers=recoveredLayerArrays;
        }
        public int[] GetLayers() {
            return layers.Y;
        }
    }
}