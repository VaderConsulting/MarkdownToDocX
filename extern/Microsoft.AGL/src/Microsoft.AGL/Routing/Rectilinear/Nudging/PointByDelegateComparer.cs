using System.Collections.Generic;
using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Routing.Rectilinear.Nudging{
    internal class PointByDelegateComparer(PointProjection projection) : IComparer<Point>{

        PointProjection projection = projection;

        /// <summary>
        /// Compares two objects and returns a value indicating whether one is less than, equal to, or greater than the other.
        /// </summary>
        /// <returns>
        /// Value 
        ///                     Condition 
        ///                     Less than zero
        ///                 <paramref name="x"/> is less than <paramref name="y"/>.
        ///                     Zero
        ///                 <paramref name="x"/> equals <paramref name="y"/>.
        ///                     Greater than zero
        ///                 <paramref name="x"/> is greater than <paramref name="y"/>.
        /// </returns>
        /// <param name="x">The first object to compare.
        ///                 </param><param name="y">The second object to compare.
        ///                 </param>
        public int Compare(Point x, Point y) => projection(x).CompareTo(projection(y));


    }
}