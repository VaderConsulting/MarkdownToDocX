using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>One usable connection point on a node side.</summary>
/// <param name="Offset">Distance from the start of the side or connection segment.</param>
/// <param name="Fraction">The same position as a fraction of the segment, as passed to a connection boundary.</param>
internal readonly record struct SideGridPoint(double Offset, double Fraction);

/// <summary>
/// The centre-out connection-point grid of term Port and rule 3.2, applied to measured sides and
/// renderer-owned connection boundaries.
/// </summary>
internal static class ConnectionPointGrid
{
    /// <summary>Fraction used to find the start of a boundary segment without touching its corner.</summary>
    private const double SEGMENT_PROBE = 0.000001;

    /// <summary>Lists the grid points on one side that the connection boundary accepts.</summary>
    /// <param name="Boundary">Renderer-owned boundary, or null for a plain rectangle side.</param>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="SideLength">Measured side length, used when there is no boundary.</param>
    /// <param name="Length">Receives the length of the side or connection segment the grid is measured on.</param>
    /// <returns>Usable points in side order.</returns>
    internal static List<SideGridPoint> UsablePoints(INodeConnectionBoundary? Boundary, string Side, double SideLength,
        out double Length)
    {
        Length = SideLength;
        List<SideGridPoint> Points = [];
        if (Boundary != null && !TryMeasureSegment(Boundary, Side, out Length))
        {
            return Points;
        }

        if (Length <= 0)
        {
            return Points;
        }

        foreach (double Offset in DiagramGridMetrics.SideGridOffsets(Length))
        {
            double Fraction = Offset / Length;
            if (Boundary == null || Boundary.TryPort(Side, Fraction, out _))
            {
                Points.Add(new SideGridPoint(Offset, Fraction));
            }
        }

        return Points;
    }

    /// <summary>Finds the default centre-out fractions for a number of lines on one boundary side.</summary>
    /// <param name="Boundary">Renderer-owned boundary, or null for a plain rectangle side.</param>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="SideLength">Measured side length, used when there is no boundary.</param>
    /// <param name="Count">Lines sharing the side.</param>
    /// <param name="Fractions">Receives the fractions in side order.</param>
    /// <returns>Whether the side's grid holds every line.</returns>
    internal static bool TryCentreOutFractions(INodeConnectionBoundary? Boundary, string Side, double SideLength, int Count,
        out double[] Fractions)
    {
        List<SideGridPoint> Points = UsablePoints(Boundary, Side, SideLength, out double Length);
        int[] Indexes = DiagramGridMetrics.CentreOutGridIndexes(Points.Select(Point => Point.Offset).ToArray(), Length, Count);
        if (Count < 1 || Indexes.Length != Count)
        {
            Fractions = [];
            return false;
        }

        Fractions = [.. Indexes.Select(Index => Points[Index].Fraction)];
        return true;
    }

    /// <summary>
    /// Places ordered lines on grid points. Fixed lines keep their points; the others take the free points
    /// nearest their default centre-out positions while keeping side order (rule 3.2 default and fallback).
    /// </summary>
    /// <param name="Grid">Usable grid offsets in side order.</param>
    /// <param name="Length">Length of the side or connection segment; its midpoint is the side centre.</param>
    /// <param name="Fixed">Grid index each line must use, or -1 when it is free; one entry per line in side order.</param>
    /// <returns>A grid index per line, or null when the grid cannot hold the lines.</returns>
    internal static int[]? Plan(IReadOnlyList<double> Grid, double Length, IReadOnlyList<int> Fixed)
    {
        int Count = Fixed.Count;
        if (Count == 0)
        {
            return [];
        }

        if (Grid.Count < Count)
        {
            return null;
        }

        int[] Defaults = DiagramGridMetrics.CentreOutGridIndexes(Grid, Length, Count);
        if (Defaults.Length != Count)
        {
            return null;
        }

        double[] Targets = [.. Defaults.Select(Index => Grid[Index])];
        int[]? Ordered = PlanInOrder(Grid, Targets, Fixed, Enumerable.Range(0, Grid.Count).ToList());
        if (Ordered != null)
        {
            return Ordered;
        }

        // A fixed straight connection may sit out of peer order. It keeps its point, and the
        // remaining lines keep their own order across the points that are left.
        HashSet<int> Taken = [.. Fixed.Where(Index => Index >= 0)];
        List<int> Free = [.. Enumerable.Range(0, Grid.Count).Where(Index => !Taken.Contains(Index))];
        int[] Unfixed = [.. Enumerable.Repeat(-1, Count)];
        List<int> FreeLines = [.. Enumerable.Range(0, Count).Where(Line => Fixed[Line] < 0)];
        int[]? Rest = PlanInOrder(Grid, FreeLines.Select(Line => Targets[Line]).ToArray(),
            Enumerable.Repeat(-1, FreeLines.Count).ToArray(), Free);
        if (Rest == null)
        {
            return null;
        }

        int[] Result = [.. Fixed];
        for (int Index = 0; Index < FreeLines.Count; Index++)
        {
            Result[FreeLines[Index]] = Rest[Index];
        }

        return Result;
    }

    /// <summary>Assigns lines to strictly increasing candidate points with the least total distance from their targets.</summary>
    /// <param name="Grid">All grid offsets.</param>
    /// <param name="Targets">Default offset per line.</param>
    /// <param name="Fixed">Required grid index per line, or -1.</param>
    /// <param name="Candidates">Grid indexes available, in side order.</param>
    /// <returns>Grid index per line, or null when no ordered assignment exists.</returns>
    private static int[]? PlanInOrder(IReadOnlyList<double> Grid, IReadOnlyList<double> Targets, IReadOnlyList<int> Fixed,
        IReadOnlyList<int> Candidates)
    {
        int Lines = Targets.Count;
        int Slots = Candidates.Count;
        if (Lines == 0)
        {
            return [];
        }

        if (Slots < Lines)
        {
            return null;
        }

        double[,] Cost = new double[Lines, Slots];
        int[,] From = new int[Lines, Slots];
        for (int Line = 0; Line < Lines; Line++)
        {
            for (int Slot = 0; Slot < Slots; Slot++)
            {
                Cost[Line, Slot] = double.PositiveInfinity;
                From[Line, Slot] = -1;
                if (Fixed[Line] >= 0 && Candidates[Slot] != Fixed[Line])
                {
                    continue;
                }

                double Own = Fixed[Line] >= 0 ? 0 : Math.Abs(Grid[Candidates[Slot]] - Targets[Line]);
                if (Line == 0)
                {
                    Cost[Line, Slot] = Own;
                    continue;
                }

                for (int Previous = 0; Previous < Slot; Previous++)
                {
                    double Total = Cost[Line - 1, Previous] + Own;
                    if (Total < Cost[Line, Slot] - 0.0000001)
                    {
                        Cost[Line, Slot] = Total;
                        From[Line, Slot] = Previous;
                    }
                }
            }
        }

        int Best = -1;
        for (int Slot = 0; Slot < Slots; Slot++)
        {
            if (!double.IsPositiveInfinity(Cost[Lines - 1, Slot])
                && (Best < 0 || Cost[Lines - 1, Slot] < Cost[Lines - 1, Best] - 0.0000001))
            {
                Best = Slot;
            }
        }

        if (Best < 0)
        {
            return null;
        }

        int[] Result = new int[Lines];
        for (int Line = Lines - 1; Line >= 0; Line--)
        {
            Result[Line] = Candidates[Best];
            Best = From[Line, Best];
        }

        return Result;
    }

    /// <summary>
    /// Returns one grid fraction per line. The fractions come from the side's fixed centre-out points,
    /// so a line's position does not move to an even share of the side when another line is added.
    /// </summary>
    /// <param name="Boundary">Renderer-owned boundary, or null for a plain rectangle side.</param>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="SideLength">Measured side length, used when there is no boundary.</param>
    /// <param name="Count">Lines sharing the side.</param>
    /// <returns>A fraction of the side per line, in side order. Every fraction is a grid point, or the centre when the side has none.</returns>
    internal static double[] FractionsOnGrid(INodeConnectionBoundary? Boundary, string Side, double SideLength, int Count)
    {
        if (Count < 1)
        {
            return [];
        }

        if (TryCentreOutFractions(Boundary, Side, SideLength, Count, out double[] Fractions))
        {
            return Fractions;
        }

        List<SideGridPoint> Points = UsablePoints(Boundary, Side, SideLength, out _);
        double[] Result = new double[Count];
        if (Points.Count == 0)
        {
            for (int Index = 0; Index < Count; Index++)
            {
                Result[Index] = 0.5;
            }

            return Result;
        }

        for (int Index = 0; Index < Count; Index++)
        {
            int PointIndex = Points.Count == 1
                ? 0
                : (int)Math.Round(Index * (Points.Count - 1) / (double)Math.Max(1, Count - 1));
            Result[Index] = Points[PointIndex].Fraction;
        }

        return Result;
    }

    /// <summary>Measures the straight connection segment a boundary exposes on one side.</summary>
    /// <param name="Boundary">Renderer-owned boundary.</param>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="Length">Receives the segment length.</param>
    /// <returns>Whether the side has a measurable segment.</returns>
    private static bool TryMeasureSegment(INodeConnectionBoundary Boundary, string Side, out double Length)
    {
        Length = 0;
        if (!Boundary.TryPort(Side, SEGMENT_PROBE, out NodeGeometryPort Start)
            || !Boundary.TryPort(Side, 0.5, out NodeGeometryPort Middle))
        {
            return false;
        }

        double Half = Math.Sqrt(Math.Pow(Middle.X - Start.X, 2) + Math.Pow(Middle.Y - Start.Y, 2));
        Length = Half / (0.5 - SEGMENT_PROBE);
        return Length > 0;
    }
}
