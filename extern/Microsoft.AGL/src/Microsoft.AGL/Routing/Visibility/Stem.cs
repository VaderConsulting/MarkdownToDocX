using System;
using System.Collections.Generic;
using Microsoft.AGL.Core.Geometry;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Visibility {
    /// <summary>
    /// represents a chunk of a hole boundary
    /// </summary>
    internal class Stem {
        PolylinePoint start;

        internal PolylinePoint Start { get => start; set => start = value;
        }
        PolylinePoint end;

        internal PolylinePoint End { get => end; set => end = value;
        }


        internal Stem(PolylinePoint start, PolylinePoint end) {
            System.Diagnostics.Debug.Assert(start.Polyline==end.Polyline);
            this.start = start;
            this.end = end;
        }

        internal IEnumerable<PolylinePoint> Sides {
            get {
                PolylinePoint v=start;
                
                while(v!= end ) {
                    PolylinePoint side = v;
                    yield return side;
                    v = side.NextOnPolyline;
                }
            }
        }

        internal bool MoveStartClockwise() {
            if (Start != End) {
                Start = Start.NextOnPolyline;
                return true;
            }
            return false;
        }

        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "Stem({0},{1})", Start, End);

    }
}
