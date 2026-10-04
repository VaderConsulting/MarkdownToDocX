using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Microsoft.AGL.DebugHelpers {
#if REPORTING
    /// <summary>
    /// support for native method calls
    /// </summary>
    public class Timer {
        [DllImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);
        [DllImport("Kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);
        private long startTime;
        private long stopTime;
        private long freq;
        /// <summary>
        /// ctor
        /// </summary>
        public Timer() {
            if (QueryPerformanceFrequency(out freq) == false) {
                throw new Win32Exception(); // timer not supanchored
            }
        }
        /// <summary>
        /// Start the timer
        /// </summary>
        /// <returns>long - tick count</returns>
        public long Start() {
            QueryPerformanceCounter(out startTime);
            return startTime;
        }
        /// <summary>
        /// Stop timer 
        /// </summary>
        /// <returns>long - tick count</returns>
        public long Stop() {
            QueryPerformanceCounter(out stopTime);
            return stopTime;
        }
        /// <summary>
        /// Return the duration of the timer in seconds.
        /// </summary>
        /// <returns>double - duration</returns>
        public double Duration => (double)(stopTime - startTime) / (double)freq;
        /// <summary>
        /// Frequency of timer (no counts in one second on this machine)
        /// </summary>
        ///<returns>long - Frequency</returns>
        public long Frequency {
            get {
                QueryPerformanceFrequency(out freq);
                return freq;
            }
        }
    }
#endif
}




