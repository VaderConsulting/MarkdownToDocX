using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered {
    internal class RecoveryLayerCalculator(LayerArrays recoveredLayerArrays) : LayerCalculator {
        LayerArrays layers = recoveredLayerArrays;

        public int[] GetLayers() => layers.Y;
    }
}