using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MermaidRecognition.Layout.Sugiyama.Core.Geometry.RectanglePacking
{
    /// <summary>
    /// A rectangle and associated data that need to be packed
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    /// <remarks>
    /// Associate a rectangle with a data item to be packed
    /// </remarks>
    /// <param name="rectangle"></param>
    /// <param name="data"></param>
    public class RectangleToPack<TData>(Rectangle rectangle, TData data)
    {
        /// <summary>
        /// Rectangle to be packed - packing will translate the rectangle
        /// </summary>
        public Rectangle Rectangle { get; internal set; } = rectangle;

        /// <summary>
        /// data associated with rectangle
        /// </summary>
        public TData Data { get; private set; } = data;
    }
}
