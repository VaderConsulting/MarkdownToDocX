using MermaidRecognition.Layout.Sugiyama.Core;
namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered {
    internal class FlatEdgeRouter : AlgorithmBase {
        readonly Routing routing;
        private SugiyamaLayoutSettings settings;
        int[][] Layers => routing.LayerArrays.Layers;

        internal FlatEdgeRouter(SugiyamaLayoutSettings settings, Routing routing)
        {
            this.settings = settings;
            this.routing = routing;
        }

        protected override void RunInternal()
        {
            for (int i = 0; i < Layers.Length; i++) {
                ProgressStep();
                RouteFlatEdgesBetweenTwoLayers(Layers[i],
                    i < Layers.Length - 1 ? Layers[i + 1] : new int[0]);
            }
        }

        private void RouteFlatEdgesBetweenTwoLayers(int[] lowerLayer, int[] upperLayer) {
            TwoLayerFlatEdgeRouter twoLayerRouter = new TwoLayerFlatEdgeRouter(settings, routing,
                lowerLayer, upperLayer);
            twoLayerRouter.Run();
        }
    }
}
