using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.AGL.Core.Geometry;

namespace Microsoft.AGL.Core.Layout
{
    /// <summary>
    /// keeps the arrowhead info
    /// </summary>
#if TEST_MSAGL
    
#endif
    public class Arrowhead
    {

        ///<summary>
        ///</summary>
        public const double DefaultArrowheadLength = 10;
        double length = DefaultArrowheadLength;

        ///<summary>
        /// The overall length of the arrow head
        ///</summary>
        public double Length { get => length; set => length = value;
        }

        ///<summary>
        /// The width of the arrow head at the base
        ///</summary>
        public double Width { get; set; }

        ///<summary>
        /// Where the tip of the arrow head is
        ///</summary>
        public Point TipPosition { get; set; }

        ///<summary>
        /// A relative offset that moves the tip position 
        ///</summary>
        public double Offset { get; set; }

        /// <summary>
        /// Clone the arrowhead information
        /// </summary>
        /// <returns></returns>
        public Arrowhead Clone() => new Arrowhead()
        {
            Length = length,
            Width = Width,
            TipPosition = TipPosition,
            Offset = Offset
        };
    }
}
