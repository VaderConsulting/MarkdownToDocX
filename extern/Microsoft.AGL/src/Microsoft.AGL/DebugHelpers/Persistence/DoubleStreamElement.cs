namespace Microsoft.AGL.DebugHelpers.Persistence
{
    internal class DoubleStreamElement : CurveStreamElement {
        public DoubleStreamElement(double res) => Value = res;

        internal double Double => (double)Value;
    }
}