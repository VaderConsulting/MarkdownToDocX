using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Layout.Layered {
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
