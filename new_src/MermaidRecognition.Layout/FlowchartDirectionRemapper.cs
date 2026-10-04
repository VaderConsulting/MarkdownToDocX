using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Layout;

/// <summary>
/// Remaps Sugiyama TB layout coordinates to the diagram's declared direction (spec §3.1).
/// </summary>
public static class FlowchartDirectionRemapper
{
    /// <summary>
    /// Transforms node centres produced by TB Sugiyama layout into the target flow direction.
    /// Label boxes stay axis-aligned (horizontal text); only rank positions are remapped.
    /// </summary>
    public static List<FlowchartLayoutNodeBounds> Remap(
        IReadOnlyList<FlowchartLayoutNodeBounds> Source,
        FlowchartDirection Direction)
    {
        if (Source.Count == 0 || Direction == FlowchartDirection.TopToBottom)
        {
            return new List<FlowchartLayoutNodeBounds>(Source);
        }

        List<WorkingBounds> Working = [];
        foreach (FlowchartLayoutNodeBounds Bounds in Source)
        {
            double CenterX = Bounds.CenterX;
            double CenterY = Bounds.CenterY;
            double Width = Bounds.Width;
            double Height = Bounds.Height;

            if (Direction == FlowchartDirection.LeftToRight
                || Direction == FlowchartDirection.RightToLeft)
            {
                CenterX = Bounds.CenterY;
                CenterY = Bounds.CenterX;
            }

            Working.Add(new WorkingBounds(Bounds.NodeId, CenterX, CenterY, Width, Height));
        }

        if (Direction == FlowchartDirection.BottomToTop)
        {
            double MinY = Working.Min(Entry => Entry.CenterY);
            double MaxY = Working.Max(Entry => Entry.CenterY);
            for (int Index = 0; Index < Working.Count; Index++)
            {
                WorkingBounds Entry = Working[Index];
                Working[Index] = new WorkingBounds(
                    Entry.NodeId,
                    Entry.CenterX,
                    MinY + MaxY - Entry.CenterY,
                    Entry.Width,
                    Entry.Height);
            }
        }
        else if (Direction == FlowchartDirection.LeftToRight)
        {
            double MinX = Working.Min(Entry => Entry.CenterX);
            double MaxX = Working.Max(Entry => Entry.CenterX);
            for (int Index = 0; Index < Working.Count; Index++)
            {
                WorkingBounds Entry = Working[Index];
                Working[Index] = new WorkingBounds(
                    Entry.NodeId,
                    MinX + MaxX - Entry.CenterX,
                    Entry.CenterY,
                    Entry.Width,
                    Entry.Height);
            }
        }

        List<FlowchartLayoutNodeBounds> Remapped = [];
        foreach (WorkingBounds Entry in Working)
        {
            Remapped.Add(new FlowchartLayoutNodeBounds
            {
                NodeId = Entry.NodeId,
                CenterX = Entry.CenterX,
                CenterY = Entry.CenterY,
                Width = Entry.Width,
                Height = Entry.Height
            });
        }

        return Remapped;
    }

    /// <summary>
    /// Computes axis-aligned graph bounds from node centres and sizes.
    /// </summary>
    public static (double Width, double Height) ComputeGraphSize(IReadOnlyList<FlowchartLayoutNodeBounds> Bounds)
    {
        if (Bounds.Count == 0)
        {
            return (0.0, 0.0);
        }

        double MinLeft = double.MaxValue;
        double MinBottom = double.MaxValue;
        double MaxRight = double.MinValue;
        double MaxTop = double.MinValue;

        foreach (FlowchartLayoutNodeBounds NodeBounds in Bounds)
        {
            double Left = NodeBounds.CenterX - (NodeBounds.Width / 2.0);
            double Right = NodeBounds.CenterX + (NodeBounds.Width / 2.0);
            double Bottom = NodeBounds.CenterY - (NodeBounds.Height / 2.0);
            double Top = NodeBounds.CenterY + (NodeBounds.Height / 2.0);

            if (Left < MinLeft)
            {
                MinLeft = Left;
            }

            if (Right > MaxRight)
            {
                MaxRight = Right;
            }

            if (Bottom < MinBottom)
            {
                MinBottom = Bottom;
            }

            if (Top > MaxTop)
            {
                MaxTop = Top;
            }
        }

        return (Math.Max(1.0, MaxRight - MinLeft), Math.Max(1.0, MaxTop - MinBottom));
    }

    private sealed class WorkingBounds(string NodeId, double CenterX, double CenterY, double Width, double Height)
    {
        public string NodeId { get; } = NodeId;

        public double CenterX { get; } = CenterX;

        public double CenterY { get; } = CenterY;

        public double Width { get; } = Width;

        public double Height { get; } = Height;
    }
}
