using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves
{
    internal class PolylineIterator:IEnumerator<Point>
    {
        Polyline polyline;

        PolylinePoint currentPolyPoint;

        internal PolylineIterator(Polyline poly) => polyline = poly;

        #region IEnumerator<Point> Members

#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=332
        public Point Current => currentPolyPoint.Point;
#else
        Point IEnumerator<Point>.Current => currentPolyPoint.Point;
#endif

        #endregion

        #region IDisposable Members

#if !SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=71
        public void Dispose() => GC.SuppressFinalize(this);
#else
        void IDisposable.Dispose() => GC.SuppressFinalize(this);
#endif

        #endregion

        #region IEnumerator Members

        object System.Collections.IEnumerator.Current => currentPolyPoint.Point;

#if SHARPKIT //https://code.google.com/p/sharpkit/issues/detail?id=332&thanks=332
        public bool MoveNext()
#else
        bool System.Collections.IEnumerator.MoveNext()
#endif
        {
            if (currentPolyPoint == null)
            {
                currentPolyPoint = polyline.StartPoint;
                return currentPolyPoint != null;
            }
            if(currentPolyPoint==polyline.EndPoint)
            {
                return false;
            }

            currentPolyPoint = currentPolyPoint.Next;
            return true;
        }

        void System.Collections.IEnumerator.Reset() => currentPolyPoint = null;

        #endregion
    }
}
