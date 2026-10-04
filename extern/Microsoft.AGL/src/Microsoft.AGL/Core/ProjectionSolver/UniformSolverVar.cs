using System.Diagnostics;

namespace Microsoft.AGL.Core.ProjectionSolver{
    internal class UniformSolverVar{
        double lowBound = double.NegativeInfinity;
        double upperBound = double.PositiveInfinity;
        internal bool IsFixed;
        double position;

        internal double Width { get; set; }

        internal double Position
        {
            get => position;
            set
            {
                if (value < lowBound)
                {
                    position = lowBound;
                }
                else if (value > upperBound)
                {
                    position = upperBound;
                }
                else
                {
                    position = value;
                }
            }
        }

        internal double LowBound
        {
            get => lowBound;
            set
            {
                Debug.Assert(value <= upperBound);
                lowBound = value;
            }
        }

        internal double UpperBound
        {
            get => upperBound;
            set
            {
                Debug.Assert(value >= LowBound);
                upperBound = value;
            }
        }



#if TEST_MSAGL
        public override string ToString() => lowBound + " " + Position + " " + upperBound;
#endif
    }
}