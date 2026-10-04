using System;
using System.Collections.Generic;

namespace Microsoft.AGL.Core.DataStructures.RBTree
{
    internal class ComparerOnDelegate<T>(Func<T, T, int> compare) : IComparer<T> {
        readonly Func<T, T, int> comparer = compare;

        public int Compare(T x, T y) => comparer(x, y);
    }
}