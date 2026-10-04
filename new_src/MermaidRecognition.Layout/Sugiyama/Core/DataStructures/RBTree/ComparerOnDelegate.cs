using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.DataStructures.RBTree
{
    internal class ComparerOnDelegate<T>(Func<T, T, int> compare) : IComparer<T> {
        readonly Func<T, T, int> comparer = compare;

        public int Compare(T? x, T? y) { if (x is null) { return y is null ? 0 : -1; } if (y is null) { return 1; } return comparer(x, y); }
    }
}