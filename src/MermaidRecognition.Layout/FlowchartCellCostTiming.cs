using System;
using System.Diagnostics;

namespace MermaidRecognition.Layout;

/// <summary>Separates nested cell-layout costs without counting child work again in its parent.</summary>
internal static class FlowchartCellCostTiming
{
    [ThreadStatic] private static Scope? Current;

    /// <summary>Starts optional inclusive and exclusive timing; disabled profiling allocates no scope or stopwatch.</summary>
    /// <param name="Name">Existing algorithm category, independent of scenario identity.</param>
    /// <returns>A scope restoring its parent, or null when stage profiling is disabled.</returns>
    internal static IDisposable? Begin(string Name) => DiagramGridLayout.LayoutStageTimingObserver == null ? null : new Scope(Name);

    /// <summary>Owns elapsed time and subtracts all instrumented children exactly once.</summary>
    private sealed class Scope : IDisposable
    {
        private readonly Scope? Parent = Current;
        private readonly string Name;
        private readonly Stopwatch Clock = Stopwatch.StartNew();
        private double Children;

        /// <summary>Links one measurement to the current nested category.</summary>
        /// <param name="Name">Frame, hint, scoring, routing or diagnostic category.</param>
        internal Scope(string Name) { this.Name = Name; Current = this; }

        /// <summary>Reports exclusive and inclusive costs, then restores the parent even for rejected candidates.</summary>
        public void Dispose()
        {
            double Total = Clock.Elapsed.TotalMilliseconds;
            Current = Parent;
            if (Parent != null) Parent.Children += Total;
            DiagramGridLayout.LayoutStageTimingObserver?.Invoke("cell-cost/exclusive/" + Name, Math.Max(0, Total - Children));
            DiagramGridLayout.LayoutStageTimingObserver?.Invoke("cell-cost/inclusive/" + Name, Total);
        }
    }
}
