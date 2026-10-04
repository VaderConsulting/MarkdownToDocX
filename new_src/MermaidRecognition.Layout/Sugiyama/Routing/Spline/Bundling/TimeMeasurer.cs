using System;
using MermaidRecognition.Layout.Sugiyama.DebugHelpers;

namespace MermaidRecognition.Layout.Sugiyama.Routing.Spline.Bundling {
    /// <summary>
    /// Outputs run time in debug mode 
    /// </summary>
    internal class TimeMeasurer {
#if DEBUG && TEST_MSAGL
        static Timer timer;
        static TimeMeasurer() {
            timer = new Timer();
            timer.Start();
        }
#endif
        /// <summary>
        /// Run the task and outputs its execution time
        /// </summary>
        internal static void Run(Task task) => Run(MethodName(task), task);

        internal static void Run(string description, Task task) {
#if DEBUG && TEST_MSAGL
            Timer t = new Timer();
            t.Start();
            task();
            t.Stop();

            Console.Write(description);
            Console.WriteLine(" executed in {0} sec.", string.Format("{0:0.00}", t.Duration));
#else
			//task();
#endif
        }

        internal delegate void Task();

        static string MethodName(Task task) {
            Type tp = task.Target.GetType().DeclaringType;
            string cls = (tp != null ? tp.Name : "");

            string method = task.Method.Name;
            if (method.Length >= 4)
                method = method.Substring(0, method.Length - 4);

            if (cls.Length > 0)
                return cls + "." + method;
            return method;
        }

        internal static void DebugOutput(string str) {
#if DEBUG && TEST_MSAGL
            timer.Stop();
            Console.Write("{0}: ", string.Format("{0:0.000}", timer.Duration));
            Console.WriteLine(str);
#endif
        }
    }
}
