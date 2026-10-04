using System;

namespace MermaidRecognition.Layout.Sugiyama.Core.DataStructures {
    /// <summary>
    /// this class behaves like one dimensional bounding box
    /// </summary>
    public class RealNumberSpan{
        internal RealNumberSpan() => IsEmpty = true;

        internal bool Intersects(RealNumberSpan a) => !(a.Max < Min || a.Min > Max);

        internal bool IsEmpty { get; set; }

        internal void AddValue(double x){
            if(IsEmpty){
                Min = Max = x;
                IsEmpty = false;
            } else if(x < Min)
                Min = x;
            else if(x > Max)
                Max = x;
        }

        internal double Min { get; set; }
        internal double Max { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public double Length => Max - Min;
#if TEST_MSAGL
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString() => IsEmpty ? "empty" : string.Format("{0},{1}", Min, Max);
#endif
    }
}