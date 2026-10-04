using System;
using System.Collections.Generic;

namespace Microsoft.AGL.Core.DataStructures.RBTree
{
    internal class DefaultComperer<T>: IComparer<T> {
        public int Compare(T x, T y) => ((IComparable<T>)x).CompareTo(y);

    }
}
