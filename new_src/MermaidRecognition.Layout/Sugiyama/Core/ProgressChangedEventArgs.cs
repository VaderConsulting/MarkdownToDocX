using System;

namespace MermaidRecognition.Layout.Sugiyama.Core
{
    /// <summary>
    /// Progress changed event argument class for MSAGL progress changes.
    /// </summary>
    /// <remarks>
    /// Constructors a ProgressChangedEventArgs with the given ratio complete and description.
    /// </remarks>
    /// <param name="ratioComplete">between 0 (not started) and 1 (finished)</param>
    /// <param name="algorithmDescription">a useful description</param>
    public class ProgressChangedEventArgs(double ratioComplete, string algorithmDescription) : EventArgs
    {
        private string algorithmDescription = algorithmDescription;
        private double ratioComplete = ratioComplete;

        /// <summary>
        /// Constructors a ProgressChangedEventArgs with the given ratio complete.
        /// </summary>
        /// <param name="ratioComplete">between 0 (not started) and 1 (finished)</param>
        public ProgressChangedEventArgs(double ratioComplete)
            : this(ratioComplete, null)
        {
        }

        /// <summary>
        /// A useful algorithm description: e.g. the stage of layout in progress.
        /// </summary>
        public string AlgorithmDescription => algorithmDescription;

        /// <summary>
        /// The ratio complete of the current stage: should always be between 0 and 1.
        /// </summary>
        public double RatioComplete => ratioComplete;
    }
}
