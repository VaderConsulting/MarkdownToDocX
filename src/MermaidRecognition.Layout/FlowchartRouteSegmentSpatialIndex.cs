using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>Uniform grid over route segments for overlap and crossing queries.</summary>
internal sealed class FlowchartRouteSegmentSpatialIndex
{
    private const double CELL_SIZE = 96.0;

    private readonly record struct Cell(int X, int Y);

    private readonly Dictionary<Cell, List<int>> _Cells = [];

    private readonly (double X, double Y)[][] _Paths;

    internal FlowchartRouteSegmentSpatialIndex((double X, double Y)[][] Paths)
    {
        _Paths = Paths;
        for (int PathIndex = 0; PathIndex < Paths.Length; PathIndex++)
        {
            (double X, double Y)[] Path = Paths[PathIndex];
            for (int Segment = 0; Segment < Path.Length - 1; Segment++)
            {
                AddSegment(PathIndex, Path[Segment].X, Path[Segment].Y, Path[Segment + 1].X, Path[Segment + 1].Y);
            }
        }
    }

    internal bool SegmentOverlapsAnyPath(double X1, double Y1, double X2, double Y2)
    {
        foreach (int PathIndex in QueryCells(X1, Y1, X2, Y2))
        {
            (double X, double Y)[] Path = _Paths[PathIndex];
            for (int Segment = 0; Segment < Path.Length - 1; Segment++)
            {
                if (FlowchartBranchRouteOrder.Intersections([(X1, Y1), (X2, Y2)],
                        [Path[Segment], Path[Segment + 1]]).Overlap > 0.001)
                {
                    return true;
                }
            }
        }

        return false;
    }

    internal IEnumerable<int> PathsOverlapping((double X, double Y)[] Path)
    {
        HashSet<int> Seen = [];
        for (int Segment = 0; Segment < Path.Length - 1; Segment++)
        {
            (double X, double Y) A = Path[Segment];
            (double X, double Y) B = Path[Segment + 1];
            foreach (int PathIndex in QueryCells(A.X, A.Y, B.X, B.Y))
            {
                if (Seen.Add(PathIndex))
                {
                    yield return PathIndex;
                }
            }
        }
    }

    private void AddSegment(int PathIndex, double X1, double Y1, double X2, double Y2)
    {
        foreach (Cell Key in CellKeys(X1, Y1, X2, Y2))
        {
            if (!_Cells.TryGetValue(Key, out List<int>? Items))
            {
                _Cells[Key] = Items = [];
            }

            Items.Add(PathIndex);
        }
    }

    private IEnumerable<int> QueryCells(double X1, double Y1, double X2, double Y2)
    {
        HashSet<int> Seen = [];
        foreach (Cell Key in CellKeys(X1, Y1, X2, Y2))
        {
            if (!_Cells.TryGetValue(Key, out List<int>? Items))
            {
                continue;
            }

            foreach (int PathIndex in Items)
            {
                if (Seen.Add(PathIndex))
                {
                    yield return PathIndex;
                }
            }
        }
    }

    private static IEnumerable<Cell> CellKeys(double X1, double Y1, double X2, double Y2)
    {
        (int MinX, int MinY, int MaxX, int MaxY) = CellRange(Math.Min(X1, X2), Math.Min(Y1, Y2), Math.Max(X1, X2), Math.Max(Y1, Y2));
        for (int CellX = MinX; CellX <= MaxX; CellX++)
        {
            for (int CellY = MinY; CellY <= MaxY; CellY++)
            {
                yield return new Cell(CellX, CellY);
            }
        }
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) CellRange(double MinX, double MinY, double MaxX, double MaxY)
    {
        return (
            (int)Math.Floor(MinX / CELL_SIZE),
            (int)Math.Floor(MinY / CELL_SIZE),
            (int)Math.Floor(MaxX / CELL_SIZE),
            (int)Math.Floor(MaxY / CELL_SIZE));
    }
}
