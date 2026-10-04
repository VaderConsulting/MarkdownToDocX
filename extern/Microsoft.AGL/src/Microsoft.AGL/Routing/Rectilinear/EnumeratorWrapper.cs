using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.AGL.Routing.Rectilinear {
    /// <summary>
    /// Used in merge-type operations to track whether MoveNext has returned false.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class EnumeratorWrapper<T> {
        /// <summary>
        /// False iff MoveNext has not been called, or has returned false.
        /// </summary>
        internal bool HasCurrent => state == MoveNextState.True;

        /// <summary>
        /// State of MoveNext.  MoveNext is not automatically called on ctor for consistency with unwrapped enumerators.
        /// </summary>
        private enum MoveNextState {
            NotCalled,
            True,
            False
        }

        private MoveNextState state = MoveNextState.NotCalled;

        private readonly IEnumerator<T> enumerator;

        internal EnumeratorWrapper(IEnumerable<T> enumerable) => enumerator = enumerable.GetEnumerator();

        internal T Current
        {
            get {
                Debug.Assert(HasCurrent, "MoveNext has not been called or has returned false");
                return enumerator.Current;
            }
        }

        internal bool MoveNext() {
            Debug.Assert(state != MoveNextState.False, "MoveNext has returned false");
            if (enumerator.MoveNext()) {
                state = MoveNextState.True;
                return true;
            } 
            state = MoveNextState.False;
            return false;
        }
    }
}