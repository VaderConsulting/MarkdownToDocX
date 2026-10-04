//
// RelativeShape.cs
// MSAGL RelativeShape class for Rectilinear Edge Routing.
//
// Copyright Microsoft Corporation.

using System;
using Microsoft.AGL.Core.Geometry.Curves;

namespace Microsoft.AGL.Routing {
    /// <summary>
    /// A shape wrapping an ICurve delegate, providing additional information.
    /// </summary>
    /// <remarks>
    /// Constructor taking the ID and the curve delegate for the shape.
    /// </remarks>
    /// <param name="curveDelegate"></param>
    public class RelativeShape(Func<ICurve> curveDelegate) : Shape {
        /// <summary>
        /// The curve of the shape.
        /// </summary>
        public override ICurve BoundaryCurve { get => curveDelegate(); set => throw new InvalidOperationException(
#if TEST_MSAGL
                                                                                   "Cannot set BoundaryCurve directly for RelativeShape"
#endif // TEST
                                                                               );
        }

        readonly Func<ICurve> curveDelegate = curveDelegate;
    }
}
