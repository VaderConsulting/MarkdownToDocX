using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.AGL.Core.Layout.ProximityOverlapRemoval.StressEnergy;
using Microsoft.AGL.Layout.MDS;

namespace Microsoft.AGL.Core.Layout.ProximityOverlapRemoval {
    /// <summary>
    /// Settings for Overlap Removal process. Usage of the properties depends on the algorithm.
    /// </summary>
    public class OverlapRemovalSettings {
        OverlapRemovalMethod method=OverlapRemovalMethod.MinimalSpanningTree;

        double epsilon = 0.01;
        int iterationsMax=1000;
        bool stopOnMaxIterat = false;
        double nodeSeparation = 4;
        int randomizationSeed = 1;
        InitialScaling initialScaling = InitialScaling.None;
         bool workInInches;

        /// <summary>
        /// Constructor.
        /// </summary>
        public OverlapRemovalSettings() => StressSettings = new StressMajorizationSettings();

        /// <summary>
        /// Settings for the StressMajorization process.
        /// </summary>
        public StressMajorizationSettings StressSettings { get; set; }

         bool randomizeAllPointsOnStart = false;
        /// <summary>
        /// If true, the overlap iteration process stops after maxIterat iterations.
        /// </summary>
        public bool StopOnMaxIterat { get => stopOnMaxIterat; set => stopOnMaxIterat = value;
        }

        /// <summary>
        /// Epsilon
        /// </summary>
        public double Epsilon { get => epsilon; set => epsilon = value;
        }

        /// <summary>
        /// Number of maxIterat to be made. In each iteration overlap is partly removed.
        /// </summary>
        public int IterationsMax { get => iterationsMax; set => iterationsMax = value;
        }

        /// <summary>
        /// Minimal distance between nodes.
        /// </summary>
        public double NodeSeparation { get => nodeSeparation; set => nodeSeparation = value;
        }


        /// <summary>
        /// 
        /// </summary>
        public int RandomizationSeed { get => randomizationSeed; set => randomizationSeed = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool RandomizeAllPointsOnStart { get => randomizeAllPointsOnStart; set => randomizeAllPointsOnStart = value;
        }

        /// <summary>
        /// Initial Scaling method is used if there is at least one edge.
        /// </summary>
        public InitialScaling InitialScaling { get => initialScaling; set => initialScaling = value;
        }

        /// <summary>
        /// Divide the coordinates by 72(Pixels) to work in inches. At the end this transformation is reverted again.
        /// </summary>
        public bool WorkInInches { get => workInInches; set => workInInches = value;
        }

        /// <summary>
        /// Method to be used for overlap removal.
        /// </summary>
        public OverlapRemovalMethod Method { get => method; set => method = value;
        }

        /// <summary>
        /// Clones the settings together with the stressmajorization settings
        /// </summary>
        /// <returns></returns>
        public OverlapRemovalSettings Clone() {
            OverlapRemovalSettings settings = new OverlapRemovalSettings
            {
                Method = Method,
                Epsilon = Epsilon,
                IterationsMax = IterationsMax,
                StopOnMaxIterat = StopOnMaxIterat,
                NodeSeparation = NodeSeparation,
                RandomizationSeed = RandomizationSeed,
                RandomizeAllPointsOnStart = randomizeAllPointsOnStart,
                InitialScaling = InitialScaling,
                WorkInInches = WorkInInches,

                StressSettings = new StressMajorizationSettings()
            };
            settings.StressSettings.MaxStressIterations = StressSettings.MaxStressIterations;
            settings.StressSettings.SolvingMethod = StressSettings.SolvingMethod;
            settings.StressSettings.UpdateMethod = StressSettings.UpdateMethod;
            settings.StressSettings.StressChangeTolerance = StressSettings.StressChangeTolerance;
            settings.StressSettings.CancelOnStressConvergence = StressSettings.CancelOnStressConvergence;
            settings.StressSettings.CancelOnStressMaxIteration = StressSettings.CancelOnStressMaxIteration;
            //relevant for conjugate gradient methods only
            settings.StressSettings.ResidualTolerance =StressSettings.ResidualTolerance;
            settings.StressSettings.CancelAfterFirstConjugate = StressSettings.CancelAfterFirstConjugate;
            settings.StressSettings.MaxSolverIterations = StressSettings.MaxSolverIterations;
            settings.StressSettings.SolverMaxIteratMethod = StressSettings.SolverMaxIteratMethod;
            settings.StressSettings.Parallelize = StressSettings.Parallelize;
            settings.StressSettings.ParallelDegree = StressSettings.ParallelDegree;
            return settings;
        }
    }
}
