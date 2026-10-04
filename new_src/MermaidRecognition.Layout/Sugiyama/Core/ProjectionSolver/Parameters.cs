// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Parameters.cs" company="Microsoft">
//   (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <summary>
// MSAGL class for Projection Solver parameters.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace MermaidRecognition.Layout.Sugiyama.Core.ProjectionSolver
{
    /// <summary>
    /// Per-instance parameters for ProjectionSolver.Solver.Solve().
    /// </summary>
    public class Parameters
#if !SILVERLIGHT
        : ICloneable
#endif
    {
        /// <summary>
        /// GapTolerance is the amount of violation of constraint gaps we will accept as a
        /// perf/accuracy tradeoff. Anything greater than this is a violation; equal or below is not.
        /// PerfAcc: setting it to a larger value yields less violations/accuracy.
        /// </summary>
        public double GapTolerance { get; set; }

        /// <summary>
        /// When the absolute difference in Qpsc function value from the previous iteration to the current
        /// iteration is below this absolute-difference threshold, or when the QpscConvergenceQuotient
        /// condition is met, the function is considered converged.  
        /// PerfAcc: setting it to a larger value yields less iterations and thus potentially lower accuracy.
        /// </summary>
        public double QpscConvergenceEpsilon { get; set; }

        /// <summary>
        /// When the absolute difference in Qpsc function value from the previous iteration to the current
        /// iteration is divided by the previous iteration's function value, if the quotient is below
        /// this value, or the QpscConvergenceEpsilon condition is met, the function is considered converged.  
        /// PerfAcc: setting it to a larger value yields less iterations and thus potentially lower accuracy;
        /// a lower value yields more iterations and potentially greater accuracy.
        /// </summary>
        public double QpscConvergenceQuotient { get; set; }

        /// <summary>
        /// The maximum number of times the outer Project/Split loop should be run.  If this is less than 0
        /// (the default) it becomes a function based upon the number of variables; if it is 0, there is no limit.
        /// Termination due to this limit will result in a feasible solution.
        /// PerfAcc:  Primarily intended to mitigate nonconvergence scenarios; modify GapTolerance instead.
        /// </summary>
        public int OuterProjectIterationsLimit { get; set; }

        /// <summary>
        /// Within any Project/Split loop iteration (see OuterProjectIterationsLimit), this is the maximum number
        /// of times Project should iterate internally.  If this is less than 0 (the default) it becomes a function
        /// based upon the number of constraints; if it is 0, there is no limit.
        /// Termination due to this limit may result in a nonfeasible solution.
        /// PerfAcc:  Primarily intended to mitigate nonconvergence scenarios; modify GapTolerance instead.
        /// </summary>
        public int InnerProjectIterationsLimit { get; set; }

        /// <summary>
        /// The maximum time (in milliseconds) allowed for ProjectionSolver.Solver.Solve(). If less than or equal
        /// to 0 (the default) there is no limit.  The cutoff is approximate since it is only examined on the outer
        /// Project iteration, for performance and to ensure a feasible result in the event of early termination.
        /// Termination due to this limit will result in a feasible solution.
        /// PerfAcc:  Primarily intended to mitigate nonconvergence scenarios; modify GapTolerance instead.
        /// </summary>
        public int TimeLimit { get; set; }

        /// <summary>
        /// Parameters for advanced options.
        /// </summary>
        public AdvancedParameters Advanced { get; private set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public Parameters()
        {
            GapTolerance = 1e-4;
            QpscConvergenceEpsilon = 1e-5;
            QpscConvergenceQuotient = 1e-6;
            OuterProjectIterationsLimit = -1;
            InnerProjectIterationsLimit = -1;
            TimeLimit = -1;
            Advanced = new AdvancedParameters();
        }

        #region ICloneable members
        /// <summary>
        /// Deep-copy the AdvancedParameters.
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            Parameters newParams = (Parameters)MemberwiseClone();
            newParams.Advanced = (AdvancedParameters)Advanced.Clone();
            return newParams;
        }
        #endregion // ICloneable members
    } // end struct Parameters

    /// <summary>
    /// Parameter specification for advanced options.
    /// </summary>
    public class AdvancedParameters
#if !SILVERLIGHT
         : ICloneable
#endif
    {
        /// <summary>
        /// Whether Solve() should use the full Qpsc (Quadratic Programming for Separation Constraints; see paper)
        /// algorithm even if there are no neighbour pairs specified (neighbour pairs will always use Qpsc).
        /// Currently this is primarily for debugging and result verification.
        /// </summary>
        public bool ForceQpsc { get; set; }

        /// <summary>
        /// Whether the full Qpsc (Quadratic Programming for Separation Constraints; see paper) algorithm
        /// should use Diagonal Scaling (see the other paper).
        /// </summary>
        public bool ScaleInQpsc { get; set; }

        /// <summary>
        /// Any Lagrangian Multiple less than (more negative than) this causes a block split.
        /// PerfAcc: setting it to a larger negative value yields less splits/accuracy.
        /// </summary>
        public double MinSplitLagrangianThreshold { get; set; }

        /// <summary>
        /// Whether to use the violation cache. PerfOnly: if false, other ViolationCache settings are ignored.
        /// </summary>
        public bool UseViolationCache { get; set; }

        /// <summary>
        /// Violation cache divisor for block count; the minimum of (number of initial blocks / ViolationCacheMinBlocksDivisor)
        /// and ViolationCacheMinBlocksCount is used as the minimum number of blocks that enables the violation cache.
        /// PerfOnly:  Modifies the number of cached violated constraints.
        /// </summary>
        public int ViolationCacheMinBlocksDivisor { get; set; }

        /// <summary>
        /// Violation cache minimum; the minimum of (number of initial blocks / ViolationCacheMinBlocksDivisor)
        /// and ViolationCacheMinBlocksCount is used as the minimum number of blocks that enables the violation cache.
        /// PerfOnly:  Modifies the number of cached violated constraints.
        /// </summary>
        // PerfOnly:  Modifies the number of cached violated constraints.
        public int ViolationCacheMinBlocksCount { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public AdvancedParameters()
        {
            ForceQpsc = false;
            ScaleInQpsc = true;
            MinSplitLagrangianThreshold = -1e-7;
            UseViolationCache = true;
            ViolationCacheMinBlocksDivisor = 10;
            ViolationCacheMinBlocksCount = 100;
        }

        #region ICloneable members
        /// <summary>
        /// Shallow-copy the object (there is nothing requiring deep-copy).
        /// </summary>
        /// <returns></returns>
        public object Clone() => MemberwiseClone();
        #endregion // ICloneable members
    }
}