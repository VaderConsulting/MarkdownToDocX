using System;

namespace Microsoft.AGL.Core.Layout {
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// constructor
    /// </remarks>
    /// <param name="previousValue"></param>
    public class IsOpenChangeEventArgs(bool previousValue) : EventArgs {
        /// <summary>
        /// 
        /// </summary>
        public readonly bool PreviousValue = previousValue;
    }
}