using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MermaidRecognition.Layout;

/// <summary>Counts actual node-write coverage for one layout invocation, independently of nested group reflows.</summary>
internal sealed class FlowchartCellWriteCoverage
{
    /// <summary>Number of nodes created by this layout invocation.</summary>
    internal int Nodes { get; set; }
    /// <summary>Number explicitly bound to declared cell regions.</summary>
    internal int BoundNodes { get; set; }
    /// <summary>All attempts to write node drawing geometry, including unbound initialization and legacy placement.</summary>
    internal long Checks { get; set; }
    /// <summary>Attempts made after cell authority was established, which exercise the rejecting guard.</summary>
    internal long BoundChecks { get; set; }
    /// <summary>Rejected direct pixel writes; each has a persistent repair-path diagnostic.</summary>
    internal long Rejections { get; set; }
}

/// <summary>Rejects writes to cell-derived pixels without changing either regions, gaps or backing fields.</summary>
internal static class FlowchartCellPixelWriteGuard
{
    /// <summary>Counts writes by detached legacy trial nodes as well as writes by nodes carrying an explicit owner.</summary>
    private static readonly AsyncLocal<FlowchartCellWriteCoverage?> ActiveCoverage = new();
    /// <summary>Test hosts explicitly enable strict behavior in Release; production hosts leave this switch unset.</summary>
    internal const string StrictTestSwitch = "MermaidRecognition.Layout.ThrowOnCellPixelWrite";

    /// <summary>Scopes coverage to one diagram invocation and restores the parent scope after a group reflow.</summary>
    /// <param name="Coverage">Counters belonging to this invocation.</param>
    /// <returns>A scope that also aggregates nested write checks without duplicating primary node counts.</returns>
    internal static IDisposable ActivateCoverage(FlowchartCellWriteCoverage Coverage)
    {
        var Previous = ActiveCoverage.Value;
        ActiveCoverage.Value = Coverage;
        return new CoverageScope(Previous, Coverage);
    }

    /// <summary>Checks a direct write and records the exact caller before throwing in strict hosts or rejecting it in Release.</summary>
    /// <param name="Node">Node whose drawing geometry was targeted.</param>
    /// <param name="Member">Coordinate, extent or legacy position method being written.</param>
    /// <returns>True only for unbound nodes, whose legacy geometry is still writable.</returns>
    /// <exception cref="InvalidOperationException">A test or Debug host attempted to write cell-derived pixels.</exception>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool AllowWrite(DiagramGridLayout.GridNode Node, string Member)
    {
        var Coverage = Node.CellWriteCoverage ?? ActiveCoverage.Value;
        if (Coverage != null) Coverage.Checks++;
        if (Node.CellNode == null) return true;
        if (Coverage != null) { Coverage.BoundChecks++; Coverage.Rejections++; }
        string Path = CallerPath();
        var Context = Node.PixelWriteContext ?? FlowchartRenderContext.Current;
        string Stage = Context.ActiveLayoutStage ?? "direct cell repair";
        var Diagnostic = new FlowchartLayoutQualityFailure("CellPixelWriteRejected",
            $"{Path} attempted {Member} on cell-bound node '{Node.Id}' during {Stage}; the write was rejected and the legal regions and gaps were kept.",
            [Node.Id], null);
        Context.RejectedCellPixelWritesMutable.Add(Diagnostic);
#if DEBUG
        const bool Strict = true;
#else
        bool Strict = AppContext.TryGetSwitch(StrictTestSwitch, out bool Enabled) && Enabled;
#endif
        if (Strict)
        {
            var Failure = new InvalidOperationException(Diagnostic.Description);
            Failure.Data["CellPixelWriteDiagnostic"] = Diagnostic;
            throw Failure;
        }
        return false;
    }

    /// <summary>Finds the repair calling the node setter, falling back to a named layout stage when optimized frames are absent.</summary>
    /// <returns>Qualified caller type and method, without including local filesystem paths.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string CallerPath()
    {
        var Methods = new StackTrace().GetFrames().Select(Frame => Frame.GetMethod())
            .Where(Method => Method?.DeclaringType != null
                && Method.DeclaringType != typeof(FlowchartCellPixelWriteGuard)
                && Method.DeclaringType != typeof(DiagramGridLayout.GridNode)).ToArray();
        if (Methods.Length == 0) return "unknown repair";
        var Callers = Methods[0]!.DeclaringType!.Assembly == typeof(DiagramGridLayout).Assembly
            ? Methods.TakeWhile(Method => Method!.DeclaringType!.Assembly == typeof(DiagramGridLayout).Assembly)
            : Methods.Take(1);
        return string.Join(" <- ", Callers.Select(Method => Method!.DeclaringType!.FullName!.Replace('+', '.') + "." + Method.Name));
    }

    /// <summary>Restores nested invocation coverage while counting all of its checks in the containing scenario.</summary>
    /// <param name="Previous">Optional parent invocation.</param>
    /// <param name="Current">Invocation being completed.</param>
    private sealed class CoverageScope(FlowchartCellWriteCoverage? Previous, FlowchartCellWriteCoverage Current) : IDisposable
    {
        /// <summary>Restores the parent and aggregates write counts, leaving primary node ownership counts independent.</summary>
        public void Dispose()
        {
            ActiveCoverage.Value = Previous;
            if (Previous == null || ReferenceEquals(Previous, Current)) return;
            Previous.Checks += Current.Checks;
            Previous.BoundChecks += Current.BoundChecks;
            Previous.Rejections += Current.Rejections;
        }
    }
}
