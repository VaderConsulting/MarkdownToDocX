using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using MermaidRecognition.Layout.Sugiyama.Core.Geometry;

#if DEBUG
using System.Linq;
#endif

namespace MermaidRecognition.Layout.Sugiyama.Routing.Rectilinear.Nudging {
    /// <summary>
    /// represents a segment of a path
    /// </summary>
    internal class LinkedPoint : IEnumerable<Point> {
        internal Point Point { get; set; }

        internal LinkedPoint Next { get; set; }

        internal LinkedPoint(Point point) => Point = point;

        public IEnumerator<Point> GetEnumerator() {
            for (LinkedPoint p = this; p != null; p = p.Next)
            {
                yield return p.Point;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal double X => Point.X;

        internal double Y => Point.Y;

        internal void InsertVerts(int i, int j, Point[] points) {
            for (j--; i < j; j--)
            {
                SetNewNext(points[j]);
            }
        }

        public void InsertVertsInReverse(int i, int j, Point[] points) {
            for (i++; i < j; i++)
            {
                SetNewNext(points[i]);
            }
        }

        internal void SetNewNext(Point p) {
            LinkedPoint nv = new LinkedPoint(p);
            LinkedPoint tmp = Next;
            Next = nv;
            nv.Next = tmp;            
            Debug.Assert( CompassVector.IsPureDirection(Point, Next.Point) );
        }

#if TEST_MSAGL
        public override string ToString() => Point.ToString();
#endif


    }
}
