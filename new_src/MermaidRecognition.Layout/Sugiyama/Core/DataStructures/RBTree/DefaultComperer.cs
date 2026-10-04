using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree
{
    internal class DefaultComperer<T>: IComparer<T> {
        public int Compare(T? x, T? y) { if (x is null) return y is null ? 0 : -1; if (y is null) return 1; return ((IComparable<T>)x).CompareTo(y); }

    }
}
