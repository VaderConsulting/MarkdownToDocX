using System;

#if PPC
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Microsoft.AGL.Core {
#if SILVERLIGHT
    public class OperationCanceledException : Exception
    {
    }
#endif


    /// <summary>
    /// a place holder for the cancelled flag
    /// </summary>
    public class CancelToken {

        volatile bool canceled;

        /// <summary>
        /// Set this flag to true when you want to cancel the layout.
        /// </summary>
        public bool Canceled
        {
            get => canceled;
            set
            {
                canceled = value;
#if PPC
                if (canceled)
                {
                    cancellationTokenSource.Cancel();
                }
#endif
            }
        }
        /// <summary>
        /// throws is the layout has been cancelled
        /// </summary>
        public void ThrowIfCanceled()
        {
#if PPC
            cancellationTokenSource.Token.ThrowIfCancellationRequested();
#endif
            if (Canceled)
            {
                throw new OperationCanceledException();
            }
        }

#if PPC
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Pass this cancelation token for use in cancellable parallel for-each operations
        /// </summary>
        public CancellationToken CancellationToken
        {
            get { return cancellationTokenSource.Token; }
        }
#endif
    }
}
