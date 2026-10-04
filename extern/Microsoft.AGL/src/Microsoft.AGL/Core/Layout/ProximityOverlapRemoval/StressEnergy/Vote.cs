namespace Microsoft.AGL.Core.Layout.ProximityOverlapRemoval.StressEnergy
{
    /// <summary>
    /// A vote for a certain distance from the node with voterIndex.
    /// </summary>
    public class Vote
    {
        /// <summary>
        /// Index of the node from which this vote is coming.
        /// </summary>
        public readonly int VoterIndex;
        /// <summary>
        /// Desired distance.
        /// </summary>
        public double Distance;
        /// <summary>
        /// Weight for this vote: usually 1/(distance*distance)" 
        /// </summary>
        public readonly double Weight;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="voterIndex"></param>
        /// <param name="distance"></param>
        /// <param name="weight"></param>
        public Vote(int voterIndex,double distance, double weight)
        {
            VoterIndex = voterIndex;
            Distance = distance;
            Weight = weight;
        }

        /// <summary>
        /// Constructor which sets the default weight for the distance.
        /// </summary>
        /// <param name="voterIndex"></param>
        /// <param name="distance"></param>
        public Vote(int voterIndex, double distance) {
            VoterIndex = voterIndex;
            Distance = distance;
            Weight = 1/distance/distance;
        }
    }
}