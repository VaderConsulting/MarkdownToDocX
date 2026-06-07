using MermaidRecognition.Layout.Sugiyama.Core.GraphAlgorithms;

namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered {
    /// <summary>
    /// the basis class for layering algorithms
    /// </summary>
    public interface LayerCalculator
    {
		/// <summary>
		/// the main method
		/// </summary>
		int[] GetLayers();
    }
}
