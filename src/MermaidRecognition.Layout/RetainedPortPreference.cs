using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

internal readonly record struct RetainedPortPreference(string Node, string Side, double X, double Y);

internal static class RetainedPortPreferences
{
    internal static int Find(string Edge, bool Source, string Node, string Side, IReadOnlyList<SideGridPoint> Points,
        Func<SideGridPoint, (double X, double Y)> Project)
    {
        if (FlowchartRenderContext.Current.PreferredPorts?.TryGetValue((Edge, Source), out var Preferred) != true
            || Preferred.Node != Node || Preferred.Side != Side) return -1;
        for (int Index = 0; Index < Points.Count; Index++)
        {
            var Actual = Project(Points[Index]);
            if (Math.Abs(Actual.X - Preferred.X) <= 0.51 && Math.Abs(Actual.Y - Preferred.Y) <= 0.51) return Index;
        }
        return -1;
    }
}
