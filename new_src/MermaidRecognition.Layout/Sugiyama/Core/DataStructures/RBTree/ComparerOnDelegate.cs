using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree
{
    internal class ComparerOnDelegate<T> : IComparer<T> {
        readonly Func<T, T, int> comparer;

        public ComparerOnDelegate(Func<T, T, int> compare) => comparer = compare;

        public int Compare(T? x, T? y) { if (x is null) return y is null ? 0 : -1; if (y is null) return 1; return comparer(x, y); }
    }
}