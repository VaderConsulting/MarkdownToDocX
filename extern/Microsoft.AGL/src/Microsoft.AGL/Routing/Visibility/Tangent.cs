using System;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing.Visibility {
    internal class Tangent {

        Tangent comp;

        //the complimentary tangent
        internal Tangent Comp { get => comp; set => comp = value;
        }

        internal bool IsHigh => !IsLow;

        bool lowTangent; //true means that it is a low tangent to Q, false meanst that it is a high tangent to Q

        internal bool IsLow { get => lowTangent; set => lowTangent = value;
        }

        bool separatingPolygons;

        internal bool SeparatingPolygons
        { get => separatingPolygons; set => separatingPolygons = value;
        }

        Diagonal diagonal;

        /// <summary>
        /// the diagonal will be not a null only when it is active
        /// </summary>
        internal Diagonal Diagonal { get => diagonal; set => diagonal = value;
        }

        PolylinePoint start;

        internal PolylinePoint Start { get => start; set => start = value;
        }

        PolylinePoint end;

        public PolylinePoint End { get => end; set => end = value;
        }

        internal Tangent(PolylinePoint start, PolylinePoint end) {
            Start = start;
            End = end;
        }

        public override string ToString() => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1}", Start, End);
    }
}
