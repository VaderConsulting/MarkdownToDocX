using Microsoft.AGL.Core.GraphAlgorithms;

namespace Microsoft.AGL.Miscellaneous.LayoutEditing
{
    internal class WeightedEdge : IEdge {
        int source;

        /// <summary>
        /// source
        /// </summary>
        public int Source { get => source; set => source = value;
        }
        int target;

        /// <summary>
        /// source
        /// </summary>
        public int Target { get => target; set => target = value;
        }

        double weight;

        internal double Weight { get => weight; set => weight = value;
        }


        internal WeightedEdge(int source, int target, double weight) {
            Source = source;
            Target = target;
            Weight = weight;
        }

    }
}
