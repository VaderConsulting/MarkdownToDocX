namespace MermaidRecognition.Layout.Sugiyama.DebugHelpers {
    internal class DoubleStreamElement : CurveStreamElement {
        public DoubleStreamElement(double res) {
            Value = res;
        }

        internal double Double { get { return (double)Value; } }
    }
}