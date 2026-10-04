namespace MermaidRecognition.Layout.Sugiyama.Layout.Layered
{
    internal class ConstrainedOrderMeasure {
        readonly int numberOfCrossings;
        //readonly double deviationFromConstraints;

        internal ConstrainedOrderMeasure(int numberOfCrossings) => this.numberOfCrossings = numberOfCrossings;//  this.deviationFromConstraints = deviationFromConstraints;

        public static bool operator <(ConstrainedOrderMeasure a, ConstrainedOrderMeasure b) => a.numberOfCrossings < b.numberOfCrossings;


        public static bool operator >(ConstrainedOrderMeasure a, ConstrainedOrderMeasure b) => b < a;

        internal bool Perfect() => numberOfCrossings == 0;
    }
}
