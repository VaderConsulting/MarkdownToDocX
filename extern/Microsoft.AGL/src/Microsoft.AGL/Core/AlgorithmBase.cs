using System;
namespace Microsoft.AGL.Core
{
    /// <summary>
    /// Base class for algorithms that are cancelable and report progress
    /// </summary>
    public abstract class AlgorithmBase
    {
        #region Running
        /// <summary>
        /// Runs the algorithm.
        /// Inherits any preexisting cancel tokens.
        /// </summary>
        public void Run()
        {
            progressRatio = 0;
            localStepCount = 0;
            
            cancelToken = threadStaticCancelToken;
            ThrowIfCanceled();

            ProgressSteps(0); // Initial progress

            RunInternal();

            if (!IsCanceled)
            {
                ProgressComplete();
            }
        }

        /// <summary>
        /// Runs the algorithm, setting up the cancel token and reverting to the old cancel token before finishing
        /// </summary>
        public void Run(CancelToken cancelToken)
        {
            progressRatio = 0;
            localStepCount = 0;

            CancelToken oldCancelToken = SetCancelToken(cancelToken);

            try
            {
                ProgressSteps(0); // Initial progress

                RunInternal();
                
                if (!IsCanceled)
                {
                    ProgressComplete();
                }
            }
            finally
            {
                threadStaticCancelToken = oldCancelToken;
            }
        }

        /// <summary>
        /// Executes the actual algorithm.
        /// </summary>
        protected abstract void RunInternal();

        #endregion Running

        #region Cancel

        /// <summary>
        /// Set the cancel token.  Returns the old cancel token, in case you need to revert at some stage.
        /// </summary>
        /// <param name="cancelToken">new cancel token</param>
        /// <returns>old cancel token</returns>
        public CancelToken SetCancelToken(CancelToken cancelToken)
        {
            this.cancelToken = cancelToken;
            ThrowIfCanceled();
            CancelToken oldCancelToken = threadStaticCancelToken;
            threadStaticCancelToken = cancelToken;
            return oldCancelToken;
        }

        /// <summary>
        /// Cancels the algorithm that is currently in progress.
        /// </summary>
        public void Cancel()
        {
            if (cancelToken != null)
            {
                cancelToken.Canceled = true;
            }
        }

        /// <summary>
        /// True if the algorithm has been canceled.
        /// </summary>
        public bool IsCanceled => cancelToken != null && cancelToken.Canceled;

        /// <summary>
        /// Throws an OperationCanceledException if the current algorithm has been canceled.
        /// </summary>
        protected void ThrowIfCanceled()
        {
            if (cancelToken != null)
            {
                cancelToken.ThrowIfCanceled();
            }
        }

        /// <summary>
        /// The current cancel token - one per thread, cached by cancelToken, below.
        /// </summary>
        [ThreadStatic]
        private static CancelToken threadStaticCancelToken;

        /// <summary>
        /// ThreadStatic field above has about 10X the lookup cost of a local field, so we
        /// cache its value in cancelToken
        /// </summary>
        private volatile CancelToken cancelToken;

        /// <summary>
        /// the current cancel token
        /// </summary>
        protected CancelToken CancelToken => cancelToken;

        #endregion Cancel

        #region Progress

        /// <summary>
        /// notifies whenever progress is made by the algorithm.  ProgressChangedEventArgs report
        /// 0 &lt; progressRatio &lt;= 1
        /// </summary>
        public event EventHandler<ProgressChangedEventArgs> ProgressChanged;

        /// <summary>
        /// Total progress to date
        /// </summary>
        private double progressRatio = 0;

        /// <summary>
        /// Step size to increment progressRatio when ProgressStep is called
        /// </summary>
        private double localProgressStepSize = LocalProgressStepSizeDefault;
        
        /// <summary>
        /// True if the progress step count has been specified using StartListenToLocalProgress.
        /// </summary>
        private bool localProgressSpecified = false;

        /// <summary>
        /// The number of times ProgressStep has been called.
        /// </summary>
        private int localStepCount = 0;

        /// <summary>
        /// When running sub-algorithms this gives the progressRatio start point
        /// </summary>
        private double stageStartRatio = 0;

        /// <summary>
        /// When running sub-algorithms this gives the progressRatio end point
        /// </summary>
        private double stageEndRatio = 1;

        /// <summary>
        /// if StartListenToLocalProgress is not used to setup step size the following will be the
        /// (rather arbitrary) default.
        /// </summary>
        private const double LocalProgressStepSizeDefault = 0.05;

        /// <summary>
        /// setup to start listening to ProgressStep() invocations from this algorithm.
        /// </summary>
        protected void StartListenToLocalProgress(int expectedSteps, double stageRatio = 1)
        {
            localProgressStepSize = stageRatio / Math.Max(1, expectedSteps); // at least 1, to prevent divide by 0
            localProgressSpecified = true;
            localStepCount = 0;
            stageStartRatio = 0;
            stageEndRatio = 1;
        }

        /// <summary>
        /// When starting a sub-algorithm start listening to its progress, transforming
        /// the progressRatio it reports (between 0 and 1) to between progressRatio and progressRatio+stageRatio
        /// </summary>
        /// <param name="childAlgorithm">The child algorithm.</param>
        /// <param name="stageRatio">The fraction of time the child algorithm will take of the parent algorithm.</param>
        protected void StartListenToProgress(AlgorithmBase childAlgorithm, double stageRatio)
        {
            ValidateArg.IsNotNull(childAlgorithm, "childAlgorithm");
            childAlgorithm.ProgressChanged += NotifyProgressChanged;
            stageStartRatio = progressRatio;
            stageEndRatio = progressRatio + stageRatio;
        }

        /// <summary>
        /// When the sub-algorithm is completed this reverts us to listening to local progress
        /// </summary>
        /// <param name="childAlgorithm"></param>
        protected void StopListenToProgress(AlgorithmBase childAlgorithm)
        {
            ValidateArg.IsNotNull(childAlgorithm, "childAlgorithm");
            childAlgorithm.ProgressChanged -= NotifyProgressChanged;
            progressRatio = stageEndRatio;
            stageStartRatio = 0;
            stageEndRatio = 1;
        }

        /// <summary>
        /// Runs the child algorithm and listens for progress changes.
        /// </summary>
        protected void RunChildAlgorithm(AlgorithmBase childAlgorithm, double stageRatio)
        {
            ValidateArg.IsNotNull(childAlgorithm, "childAlgorithm");
            try
            {
                StartListenToProgress(childAlgorithm, stageRatio);
                childAlgorithm.Run();
            }
            finally
            {
                StopListenToProgress(childAlgorithm);
            }
        }

        /// <summary>
        /// Call this to report 100% progress if your algorithm finishes early
        /// </summary>
        protected void ProgressComplete()
        {
            ThrowIfCanceled();
            if (progressRatio != 1.0)
            {
                progressRatio = 1.0;
                NotifyProgressChanged(this, new ProgressChangedEventArgs(progressRatio));
            }
        }

        /// <summary>
        /// Checks for cancel, increases progress, and notifies any listeners of progress.
        /// Call whenever the algorithm makes progress.
        /// </summary>
        protected void ProgressStep() => ProgressSteps(1);

        /// <summary>
        /// Checks for cancel, increases progress, and notifies any listeners of progress.
        /// Call whenever the algorithm makes progress.
        /// </summary>
        protected void ProgressSteps(int stepsTaken)
        {
            ThrowIfCanceled();
            localStepCount += stepsTaken;

            if (localProgressSpecified)
            {
                progressRatio = progressRatio + (localProgressStepSize * stepsTaken);
            }
            else
            {
                // If no local step count was specified, use a progress bar
                // which never reaches 100% and gets slower as time goes on.
                // It approaches 85% (instead of 100%) so it never appears to be completed.
                const double Limit = 0.85;
                const double HalfLife = 50.0; // 50 was chosen as the default since it represents one ProgressStep per percentage at the start (i.e. 50 steps reach 50%)
                const double Numerator = Limit * HalfLife;
                progressRatio = Limit - (Numerator / (HalfLife + localStepCount));
            }

            // Round up to 100% if the sum of steps doesn't quite reach it.
            if (Math.Round(progressRatio, 6) == 1.0)
            {
                progressRatio = 1.0;
            }

            NotifyProgressChanged(this, new ProgressChangedEventArgs(progressRatio));
        }

        /// <summary>
        /// notifies whenever progress is made by the algorithm.  ProgressChangedEventArgs report
        /// 0 &lt; progressRatio &lt;= 1.  Progress from sub-algorithms are converted to the correct ratio
        /// of the local progress
        /// </summary>
        private void NotifyProgressChanged(object sender, ProgressChangedEventArgs args)
        {
            if (ProgressChanged != null)
            {
                double stageRatio = stageEndRatio - stageStartRatio;
                double stageProgress = stageRatio * args.RatioComplete;
                ProgressChanged(this, new ProgressChangedEventArgs(stageStartRatio + stageProgress));
            }
        }

        #endregion Progress Logic
    }
}
