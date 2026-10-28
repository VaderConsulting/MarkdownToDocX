using System;
using System.Diagnostics.CodeAnalysis;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.Curves {
	/// <summary>
	/// 
	/// </summary>
#if TEST_MSAGL
    
#endif
	public class PolylinePoint {
		/// <summary>
		/// 
		/// </summary>
		Point point;

		/// <summary>
		/// 
		/// </summary>
		public Point Point
        {
            get => point;
            set
            {
                point = value;
                Polyline?.RequireInit();
            }
        }


        PolylinePoint next;

		/// <summary>
		/// 
		/// </summary>
		public PolylinePoint Next
        {
            get => next;
            set
            {
                next = value;
                Polyline?.RequireInit();
            }
        }

        PolylinePoint prev;

        /// <summary>
        /// 
        /// </summary>
        public PolylinePoint Prev
        {
            get => prev;
            set
            {
                prev = value;
                Polyline?.RequireInit();
            }
        }

        internal PolylinePoint() {
        }

        /// <summary>
        /// 
        /// </summary>
        public PolylinePoint(Point p) => Point = p;

        Polyline polyline;

        /// <summary>
        /// 
        /// </summary>
        public Polyline Polyline { get => polyline; set => polyline = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public override string ToString() => point.ToString();

        /// <summary>
        /// 
        /// </summary>
        public PolylinePoint NextOnPolyline => Polyline.Next(this);

        /// <summary>
        /// 
        /// </summary>
        public PolylinePoint PrevOnPolyline => Polyline.Prev(this);

    }
}