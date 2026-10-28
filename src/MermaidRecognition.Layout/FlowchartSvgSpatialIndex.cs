using System;
using System.Collections.Generic;

namespace MermaidRecognition.Layout;

/// <summary>Uniform-grid broad phase shared by SVG connector and label collision checks.</summary>
internal sealed class FlowchartSvgSpatialIndex
{
    private const double CELL_PX = 64.0;
    private readonly Dictionary<(int X, int Y), List<(double X, double Y)>> _points = [];
    private readonly Dictionary<(int X, int Y), List<(int Id, FlowchartSvgRect Rect)>> _rectangles = [];
    private int _nextRectangleId;

    public int PointCount { get; private set; }

    public void AddPoint(double X, double Y)
    {
        (int X, int Y) Cell = CellAt(X, Y);
        if (!_points.TryGetValue(Cell, out List<(double X, double Y)>? Items))
        {
            _points[Cell] = Items = [];
        }

        Items.Add((X, Y));
        PointCount++;
    }

    public void AddRectangle(FlowchartSvgRect Rect)
    {
        int Id = _nextRectangleId++;
        ForCells(Rect, Cell =>
        {
            if (!_rectangles.TryGetValue(Cell, out List<(int Id, FlowchartSvgRect Rect)>? Items))
            {
                _rectangles[Cell] = Items = [];
            }

            Items.Add((Id, Rect));
        });
    }

    public bool ContainsPoint(FlowchartSvgRect Query)
    {
        bool Found = false;
        ForCells(Query, Cell =>
        {
            if (Found || !_points.TryGetValue(Cell, out List<(double X, double Y)>? Items))
            {
                return;
            }

            foreach ((double X, double Y) Point in Items)
            {
                if (Query.Contains(Point.X, Point.Y)) { Found = true; return; }
            }
        });
        return Found;
    }

    public bool IntersectsRectangle(FlowchartSvgRect Query)
    {
        bool Found = false;
        HashSet<int> Seen = [];
        ForCells(Query, Cell =>
        {
            if (Found || !_rectangles.TryGetValue(Cell, out List<(int Id, FlowchartSvgRect Rect)>? Items))
            {
                return;
            }

            foreach ((int Id, FlowchartSvgRect Rect) Item in Items)
            {
                if (!Seen.Add(Item.Id))
                {
                    continue;
                }

                if (Query.Intersects(Item.Rect)) { Found = true; return; }
            }
        });
        return Found;
    }

    private static (int X, int Y) CellAt(double X, double Y) =>
        ((int)Math.Floor(X / CELL_PX), (int)Math.Floor(Y / CELL_PX));

    private static void ForCells(FlowchartSvgRect Rect, Action<(int X, int Y)> Visit)
    {
        (int MinX, int MinY) = CellAt(Rect.Left, Rect.Top);
        (int MaxX, int MaxY) = CellAt(Rect.Right, Rect.Bottom);
        for (int X = MinX; X <= MaxX; X++)
        {
            for (int Y = MinY; Y <= MaxY; Y++)
            {
                Visit((X, Y));
            }
        }
    }
}
