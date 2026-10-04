using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.Layout.ProximityOverlapRemoval.StressEnergy
{
    /// <summary>
    /// Votes are separated into block, so that their weight (BlockWeight) can easily be adjusted
    /// for different force types. 
    /// </summary>
    /// <remarks>
    /// Constructor
    /// </remarks>
    /// <param name="votings"></param>
    /// <param name="blockWeight"></param>
    public class VoteBlock(List<Vote> votings, double blockWeight)
    {
        /// <summary>
        /// Set of votes from different nodes for the node to which this block belongs.
        /// </summary>
        public List<Vote> Votings { get; set; } = votings;
        /// <summary>
        /// Defines how strong this block will be considered in the optimization process.
        /// </summary>
        public double BlockWeight { get; set; } = blockWeight;
    }
}