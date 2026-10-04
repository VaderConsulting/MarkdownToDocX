using MermaidRecognition.Layout.Sugiyama.Core.Layout;

namespace MermaidRecognition.Layout.Sugiyama.Miscellaneous.Phylo
{
    /// <summary>
    /// Phylogenetic edge: an edge with a specified length
    /// </summary>
    /// <remarks>
    /// constructor
    /// </remarks>
    /// <param name="sourceP"></param>
    /// <param name="targetP"></param>
    public class PhyloEdge(Node sourceP, Node targetP) : Edge(sourceP, targetP) {
    }
}
