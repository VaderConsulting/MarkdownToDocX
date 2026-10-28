using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MermaidRecognition.Layout;

/// <summary>Cell index for exact follow-up checks during a route repair stage.</summary>
internal sealed class FlowchartRouteSpatialIndex
{
    private const int CELL_SIZE = 120;
    private const int CELL_PADDING = 1;
    private readonly record struct Cell(int X, int Y);

    private readonly Dictionary<Cell, HashSet<FlowchartLayoutNodeBounds>> _boxes = [];
    private readonly Dictionary<FlowchartLayoutNodeBounds, HashSet<Cell>> _boxCells = [];
    private readonly Dictionary<Cell, HashSet<FlowchartLayoutEdgeRoute>> _routes = [];
    private readonly Dictionary<FlowchartLayoutEdgeRoute, HashSet<Cell>> _routeCells = [];
    private readonly Dictionary<Cell, string> _cellSignatures = [];

    internal FlowchartRouteSpatialIndex(FlowchartLayoutResult Layout)
    {
        foreach (FlowchartLayoutNodeBounds Box in Layout.NodeBounds)
        {
            AddBox(Box);
        }

        foreach (FlowchartLayoutEdgeRoute Route in Layout.EdgeRoutes)
        {
            AddRoute(Route);
        }
    }

    internal IReadOnlyCollection<FlowchartLayoutNodeBounds> QueryBoxes(
        IReadOnlyList<FlowchartLayoutPoint> Path)
    {
        HashSet<FlowchartLayoutNodeBounds> Found = [];
        foreach (Cell Cell in CellsForPath(Path))
        {
            if (_boxes.TryGetValue(Cell, out HashSet<FlowchartLayoutNodeBounds>? Items))
            {
                Found.UnionWith(Items);
            }
        }

        return Found;
    }

    internal IReadOnlyCollection<FlowchartLayoutNodeBounds> QueryBoxes(
        double Left, double Top, double Right, double Bottom)
    {
        HashSet<FlowchartLayoutNodeBounds> Found = [];
        (int MinX, int MinY, int MaxX, int MaxY) = CellRange(Left, Top, Right, Bottom);
        for (int X = MinX; X <= MaxX; X++)
        {
            for (int Y = MinY; Y <= MaxY; Y++)
            {
                if (_boxes.TryGetValue(new Cell(X, Y), out HashSet<FlowchartLayoutNodeBounds>? Items))
                {
                    Found.UnionWith(Items);
                }
            }
        }

        return Found;
    }

    internal IReadOnlyCollection<FlowchartLayoutEdgeRoute> QueryRoutes(
        IReadOnlyList<FlowchartLayoutPoint> Path, FlowchartLayoutEdgeRoute? Exclude = null)
    {
        HashSet<FlowchartLayoutEdgeRoute> Found = [];
        foreach (Cell Cell in CellsForPath(Path))
        {
            if (_routes.TryGetValue(Cell, out HashSet<FlowchartLayoutEdgeRoute>? Items))
            {
                Found.UnionWith(Items);
            }
        }

        if (Exclude != null)
        {
            Found.Remove(Exclude);
        }

        return Found;
    }

    internal void ReplaceRoute(FlowchartLayoutEdgeRoute Route)
    {
        RemoveRoute(Route);
        AddRoute(Route);
    }

    internal void ReplaceBox(FlowchartLayoutNodeBounds Box)
    {
        RemoveBox(Box);
        AddBox(Box);
    }

    internal string DependencySignature(IReadOnlyList<FlowchartLayoutPoint> Path)
    {
        List<Cell> Cells = [.. CellsForPath(Path).OrderBy(Cell => Cell.X).ThenBy(Cell => Cell.Y)];
        StringBuilder Signature = new();
        foreach (Cell Cell in Cells)
        {
            Signature.Append(Cell.X).Append(',').Append(Cell.Y).Append(':')
                .Append(GetCellSignature(Cell)).Append('|');
        }

        return Signature.ToString();
    }

    private string GetCellSignature(Cell Cell)
    {
        if (_cellSignatures.TryGetValue(Cell, out string? Cached))
        {
            return Cached;
        }

        StringBuilder Signature = new();
        if (_boxes.TryGetValue(Cell, out HashSet<FlowchartLayoutNodeBounds>? Boxes))
        {
            foreach (string Box in Boxes.Select(BoxKey).OrderBy(Key => Key, StringComparer.Ordinal))
            {
                Signature.Append('B').Append(Box).Append(';');
            }
        }

        if (_routes.TryGetValue(Cell, out HashSet<FlowchartLayoutEdgeRoute>? Routes))
        {
            foreach (string Route in Routes.Select(RouteKey).OrderBy(Key => Key, StringComparer.Ordinal))
            {
                Signature.Append('R').Append(Route).Append(';');
            }
        }

        Cached = Signature.ToString();
        _cellSignatures[Cell] = Cached;
        return Cached;
    }

    private static string BoxKey(FlowchartLayoutNodeBounds Box) => Box.NodeId + ":"
        + Bits(Box.CenterX) + "," + Bits(Box.CenterY) + "," + Bits(Box.Width) + "," + Bits(Box.Height);

    private static string RouteKey(FlowchartLayoutEdgeRoute Route) => Route.EdgeId + ":" + Route.ExitSide
        + ":" + Route.EntrySide + ":" + string.Join(";", Route.Points.Select(Point =>
            Bits(Point.X) + "," + Bits(Point.Y)));

    private static string Bits(double Value) => BitConverter.DoubleToInt64Bits(Value).ToString("X16");

    private void AddBox(FlowchartLayoutNodeBounds Box)
    {
        double Padding = DiagramGridMetrics.GRID_UNIT_PX / 5.0;
        HashSet<Cell> Occupied = BoxCells(Box, Box.CenterX - (Box.Width / 2.0) - Padding,
            Box.CenterY - (Box.Height / 2.0) - Padding,
            Box.CenterX + (Box.Width / 2.0) + Padding,
            Box.CenterY + (Box.Height / 2.0) + Padding);
        _boxCells[Box] = Occupied;
        foreach (Cell Key in Occupied)
        {
            if (!_boxes.TryGetValue(Key, out HashSet<FlowchartLayoutNodeBounds>? Items))
            {
                _boxes[Key] = Items = [];
            }

            Items.Add(Box);
            _cellSignatures.Remove(Key);
        }
    }

    private void RemoveBox(FlowchartLayoutNodeBounds Box)
    {
        if (!_boxCells.Remove(Box, out HashSet<Cell>? Occupied))
        {
            return;
        }

        foreach (Cell Key in Occupied)
        {
            if (!_boxes.TryGetValue(Key, out HashSet<FlowchartLayoutNodeBounds>? Items))
            {
                continue;
            }

            Items.Remove(Box);
            if (Items.Count == 0)
            {
                _boxes.Remove(Key);
            }

            _cellSignatures.Remove(Key);
        }
    }

    private static HashSet<Cell> BoxCells(FlowchartLayoutNodeBounds Box, double Left, double Top,
        double Right, double Bottom)
    {
        HashSet<Cell> Cells = [];
        (int MinX, int MinY, int MaxX, int MaxY) = CellRange(Left, Top, Right, Bottom);
        for (int X = MinX; X <= MaxX; X++)
        {
            for (int Y = MinY; Y <= MaxY; Y++)
            {
                Cells.Add(new Cell(X, Y));
            }
        }

        return Cells;
    }

    private void AddRoute(FlowchartLayoutEdgeRoute Route)
    {
        HashSet<Cell> Occupied = CellsForPath(Route.Points);
        _routeCells[Route] = Occupied;
        foreach (Cell Cell in Occupied)
        {
            if (!_routes.TryGetValue(Cell, out HashSet<FlowchartLayoutEdgeRoute>? Items))
            {
                _routes[Cell] = Items = [];
            }

            Items.Add(Route);
            _cellSignatures.Remove(Cell);
        }
    }

    private void RemoveRoute(FlowchartLayoutEdgeRoute Route)
    {
        if (!_routeCells.Remove(Route, out HashSet<Cell>? Occupied))
        {
            return;
        }

        foreach (Cell Cell in Occupied)
        {
            if (!_routes.TryGetValue(Cell, out HashSet<FlowchartLayoutEdgeRoute>? Items))
            {
                continue;
            }

            Items.Remove(Route);
            if (Items.Count == 0)
            {
                _routes.Remove(Cell);
            }

            _cellSignatures.Remove(Cell);
        }
    }

    private static HashSet<Cell> CellsForPath(IReadOnlyList<FlowchartLayoutPoint> Points)
    {
        HashSet<Cell> Result = [];
        if (Points.Count == 1)
        {
            AddSegment(Result, Points[0].X, Points[0].Y, Points[0].X, Points[0].Y);
        }

        for (int Index = 1; Index < Points.Count; Index++)
        {
            AddSegment(Result, Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y);
        }

        return Result;
    }

    private static void AddSegment(HashSet<Cell> Cells, double X1, double Y1, double X2, double Y2)
    {
        (int MinX, int MinY, int MaxX, int MaxY) = CellRange(X1, Y1, X2, Y2);
        for (int X = MinX; X <= MaxX; X++)
        {
            for (int Y = MinY; Y <= MaxY; Y++)
            {
                Cells.Add(new Cell(X, Y));
            }
        }
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) CellRange(
        double X1, double Y1, double X2, double Y2)
    {
        int MinX = FloorCell(Math.Min(X1, X2)) - CELL_PADDING;
        int MinY = FloorCell(Math.Min(Y1, Y2)) - CELL_PADDING;
        int MaxX = FloorCell(Math.Max(X1, X2)) + CELL_PADDING;
        int MaxY = FloorCell(Math.Max(Y1, Y2)) + CELL_PADDING;
        return (MinX, MinY, MaxX, MaxY);
    }

    private static int FloorCell(double Value) => (int)Math.Floor(Value / CELL_SIZE);
}
