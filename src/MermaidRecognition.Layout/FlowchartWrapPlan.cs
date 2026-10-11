using System;

namespace MermaidRecognition.Layout;

/// <summary>Chooses a grid wrap from measured node sizes and rule gaps.</summary>
internal static class FlowchartWrapPlan
{
    internal const int MinimumRun = 4;
    private const double TargetRatio = 4.0 / 3.0;

    internal readonly record struct Plan(int Columns, int Rows, int Width, int Height)
    {
        internal double Ratio => Height == 0 ? 0.0 : Width / (double)Height;
    }

    internal static Plan Choose(int NodeCount, int NodeWidth, int NodeHeight,
        int ColumnGap, int RankGap, bool Horizontal)
    {
        if (NodeCount < 1)
        {
            return default;
        }

        int FirstRun = Math.Min(NodeCount, MinimumRun);
        Plan Best = Horizontal
            ? Measure(NodeCount, FirstRun, NodeWidth, NodeHeight, ColumnGap, RankGap)
            : Measure(NodeCount, DivideRoundUp(NodeCount, FirstRun), NodeWidth, NodeHeight, ColumnGap, RankGap);
        double BestDistance = RatioDistance(Best);

        for (int Run = FirstRun + 1; Run <= NodeCount; Run++)
        {
            int Columns = Horizontal ? Run : DivideRoundUp(NodeCount, Run);
            Plan Candidate = Measure(NodeCount, Columns, NodeWidth, NodeHeight, ColumnGap, RankGap);
            double Distance = RatioDistance(Candidate);
            if (Distance < BestDistance - 1e-9
                || (Math.Abs(Distance - BestDistance) <= 1e-9 && Candidate.Width * (long)Candidate.Height < Best.Width * (long)Best.Height))
            {
                Best = Candidate;
                BestDistance = Distance;
            }
        }

        return Best;
    }

    private static Plan Measure(int NodeCount, int Columns, int NodeWidth, int NodeHeight,
        int ColumnGap, int RankGap)
    {
        Columns = Math.Max(1, Math.Min(NodeCount, Columns));
        int Rows = DivideRoundUp(NodeCount, Columns);
        int Width = Columns * NodeWidth + Math.Max(0, Columns - 1) * ColumnGap;
        int Height = Rows * NodeHeight + Math.Max(0, Rows - 1) * RankGap;
        return new Plan(Columns, Rows, Width, Height);
    }

    private static int DivideRoundUp(int Dividend, int Divisor) => (Dividend + Divisor - 1) / Divisor;

    private static double RatioDistance(Plan Candidate) => Math.Abs(Math.Log(Candidate.Ratio / TargetRatio));
}
