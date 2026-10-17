using System;
using System.Collections.Generic;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Measures actual allocated port separation; relationship counts alone never request node growth.</summary>
internal static class FlowchartPortCapacity
{
    /// <summary>Finds the geometric shortfall between distinct allocated ports on a connection edge.</summary>
    /// <param name="Ports">Actual local connection-boundary points in allocation order.</param>
    /// <param name="MinimumSeparation">Required pixel separation from rule 3.2.</param>
    /// <returns>Missing separation in pixels; zero for a legal allocation, including zero or one port.</returns>
    internal static double Shortfall(IReadOnlyList<NodeGeometryPort> Ports, double MinimumSeparation)
    {
        ArgumentNullException.ThrowIfNull(Ports);
        if (!double.IsFinite(MinimumSeparation) || MinimumSeparation <= 0)
            throw new ArgumentOutOfRangeException(nameof(MinimumSeparation));
        double Missing = 0;
        for (int First = 0; First < Ports.Count; First++)
        {
            if (!double.IsFinite(Ports[First].X) || !double.IsFinite(Ports[First].Y))
                throw new ArgumentException("Allocated ports must have finite coordinates.", nameof(Ports));
            for (int Second = 0; Second < First; Second++)
            {
                double Dx = Ports[First].X - Ports[Second].X, Dy = Ports[First].Y - Ports[Second].Y;
                Missing = Math.Max(Missing, MinimumSeparation - Math.Sqrt(Dx * Dx + Dy * Dy));
            }
        }
        return Missing;
    }
}
