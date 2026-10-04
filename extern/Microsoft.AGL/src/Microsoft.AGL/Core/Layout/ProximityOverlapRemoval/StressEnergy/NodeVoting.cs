using System.Collections.Generic;

namespace Microsoft.AGL.Core.Layout.ProximityOverlapRemoval.StressEnergy {
    /// <summary>
    /// Collection of voting blocks for the node with index VotedNodeIndex.
    /// </summary>
    public class NodeVoting
    {
        /// <summary>
        /// Index of the voted node.
        /// </summary>
        readonly public int VotedNodeIndex;

        /// <summary>
        /// List of Blocks. Each Block has a set of votes for this node.
        /// </summary>
        public List<VoteBlock> VotingBlocks { get; set;}

        /// <summary>
        /// Constructor which initializes a single empty block with BlockWeight 1.
        /// </summary>
        /// <param name="votedNodeIndex"></param>
        public NodeVoting(int votedNodeIndex) {
            VotedNodeIndex = votedNodeIndex;
            VotingBlocks=[];

            VoteBlock voteBlock = new VoteBlock([], 1); 
            VotingBlocks.Add(voteBlock);
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="votedNodeIndex"></param>
        /// <param name="votingBlocks"></param>
        public NodeVoting(int votedNodeIndex, List<VoteBlock> votingBlocks)
        {
            VotedNodeIndex = votedNodeIndex;
            VotingBlocks = votingBlocks;
        }
    }
}