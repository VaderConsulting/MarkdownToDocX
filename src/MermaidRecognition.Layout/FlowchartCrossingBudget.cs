using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Reports the real crossing count and the structural forced minimum used by rule 2.13.</summary>
public static class FlowchartCrossingBudget
{
    public static (int Crossings, int ForcedMinimum) Measure(DiagramContract Contract, FlowchartLayoutResult Layout) =>
        (FlowchartLayoutScore.CountCrossings(Layout), FlowchartLayoutScore.ForcedCrossingMinimum(Contract, Layout));
}
