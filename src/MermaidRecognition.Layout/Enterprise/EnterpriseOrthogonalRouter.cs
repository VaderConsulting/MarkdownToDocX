using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Orthogonal obstacle-aware router for enterprise edges. Node bodies, compound titles
/// and group tabs are obstacles. Group and compound borders are crossed at a right angle
/// through a gate away from the tab or title.
/// </summary>
internal static class EnterpriseOrthogonalRouter
{
    private const double CELL_SIZE = 8;

    private const double WORLD_MARGIN = 72;

    private const int TURN_COST = 3;

    private const int USED_CELL_COST = 6;

    private const double STUB = 10;

    internal static List<(double X, double Y)> Route(
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        EnterprisePort SourcePort,
        EnterprisePort TargetPort,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        HashSet<long> UsedCells)
    {
        List<EnterprisePort> Waypoints = BuildWaypoints(Source, Target, SourcePort, TargetPort, Nodes);
        List<EnterpriseLayoutNode> ExitOrder = EnterpriseObstacleCatalog.CrossingContainers(Source, Target, Nodes);
        HashSet<string> ClosedContainers = new HashSet<string>(StringComparer.Ordinal);
        List<(double X, double Y)> Combined = new List<(double X, double Y)>();
        for (int Index = 0; Index < Waypoints.Count - 1; Index++)
        {
            EnterprisePort From = Waypoints[Index];
            EnterprisePort To = Waypoints[Index + 1];
            List<EnterpriseRect> Obstacles = EnterpriseObstacleCatalog.ObstaclesForHop(
                Source,
                Target,
                Nodes,
                ClosedContainers);
            List<(double X, double Y)> Hop = RouteHop(From, To, Obstacles, Nodes, UsedCells);
            AppendHop(Combined, Hop);
            if (Index < ExitOrder.Count)
            {
                ClosedContainers.Add(ExitOrder[Index].Id);
            }
        }

        if (Combined.Count < 2)
        {
            Combined.Clear();
            Combined.Add((SourcePort.X, SourcePort.Y));
            Combined.Add((TargetPort.X, TargetPort.Y));
        }

        MarkUsed(Combined, UsedCells);
        return Simplify(Combined);
    }

    internal static string BuildPolylinePathData(List<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return string.Empty;
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append('M')
            .Append(EnterpriseSvgFormat.Number(Points[0].X))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(Points[0].Y));
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Builder.Append('L')
                .Append(EnterpriseSvgFormat.Number(Points[Index].X))
                .Append(',')
                .Append(EnterpriseSvgFormat.Number(Points[Index].Y));
        }

        return Builder.ToString();
    }

    internal static (double X, double Y) LabelAnchor(List<(double X, double Y)> Points)
    {
        if (Points.Count < 2)
        {
            return (0, 0);
        }

        int Best = 0;
        double BestLength = -1;
        for (int Index = 0; Index < Points.Count - 1; Index++)
        {
            double Dx = Points[Index + 1].X - Points[Index].X;
            double Dy = Points[Index + 1].Y - Points[Index].Y;
            double Length = Math.Abs(Dx) + Math.Abs(Dy);
            if (Length > BestLength)
            {
                BestLength = Length;
                Best = Index;
            }
        }

        double MidX = (Points[Best].X + Points[Best + 1].X) / 2.0;
        double MidY = (Points[Best].Y + Points[Best + 1].Y) / 2.0;
        double Sx = Points[Best + 1].X - Points[Best].X;
        double Sy = Points[Best + 1].Y - Points[Best].Y;
        if (Math.Abs(Sx) >= Math.Abs(Sy))
        {
            return (MidX, MidY - 10);
        }

        return (MidX + 10, MidY);
    }

    internal static bool CrossesContainer(
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        return EnterpriseObstacleCatalog.CrossingContainers(Source, Target, Nodes).Count > 0
            || EnterpriseObstacleCatalog.CrossingContainers(Target, Source, Nodes).Count > 0;
    }

    private static List<EnterprisePort> BuildWaypoints(
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        EnterprisePort SourcePort,
        EnterprisePort TargetPort,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<EnterprisePort> Waypoints = new List<EnterprisePort>();
        Waypoints.Add(SourcePort);
        List<EnterpriseLayoutNode> Exits = EnterpriseObstacleCatalog.CrossingContainers(Source, Target, Nodes);
        foreach (EnterpriseLayoutNode Container in Exits)
        {
            Waypoints.Add(ChooseGate(Container, Source, Target, true));
        }

        List<EnterpriseLayoutNode> Entries = EnterpriseObstacleCatalog.CrossingContainers(Target, Source, Nodes);
        Entries.Reverse();
        foreach (EnterpriseLayoutNode Container in Entries)
        {
            Waypoints.Add(ChooseGate(Container, Target, Source, false));
        }

        Waypoints.Add(TargetPort);
        return Waypoints;
    }

    private static EnterprisePort ChooseGate(
        EnterpriseLayoutNode Container,
        EnterpriseLayoutNode Inner,
        EnterpriseLayoutNode Outer,
        bool Exiting)
    {
        EnterpriseRect Bounds = EnterpriseObstacleCatalog.BoundsOf(Container);
        EnterpriseRect InnerBounds = EnterpriseObstacleCatalog.BoundsOf(Inner);
        EnterprisePortSide Side = FacingSide(Bounds, EnterpriseObstacleCatalog.BoundsOf(Outer).CentreX, EnterpriseObstacleCatalog.BoundsOf(Outer).CentreY);
        if (Container.IsCompound && !Container.IsGroup && Side == EnterprisePortSide.Top)
        {
            Side = Outer.AbsoluteX + (Outer.Width / 2.0) < Bounds.CentreX
                ? EnterprisePortSide.Left
                : EnterprisePortSide.Right;
        }

        if (Container.IsGroup && Side == EnterprisePortSide.Top)
        {
            EnterpriseRect? Tab = EnterpriseObstacleCatalog.GroupTabOf(Container);
            double TabRight = Tab.HasValue ? Tab.Value.Right + 12 : Bounds.X + 12;
            double X = Clamp(InnerBounds.CentreX, TabRight, Bounds.Right - 12);
            double Y = Bounds.Y + EnterpriseObstacleCatalog.GROUP_TAB_HEIGHT;
            if (Exiting)
            {
                Y -= 2;
            }
            else
            {
                Y += 2;
            }

            return new EnterprisePort(X, Y, EnterprisePortSide.Top);
        }

        return PortOnSide(Bounds, Side, InnerBounds.CentreX, InnerBounds.CentreY, Exiting);
    }

    private static EnterprisePort PortOnSide(
        EnterpriseRect Bounds,
        EnterprisePortSide Side,
        double PreferX,
        double PreferY,
        bool Exiting)
    {
        double Out = Exiting ? 2 : -2;
        switch (Side)
        {
            case EnterprisePortSide.Top:
                return new EnterprisePort(Clamp(PreferX, Bounds.X + 12, Bounds.Right - 12), Bounds.Y - Out, Side);
            case EnterprisePortSide.Bottom:
                return new EnterprisePort(Clamp(PreferX, Bounds.X + 12, Bounds.Right - 12), Bounds.Bottom + Out, Side);
            case EnterprisePortSide.Left:
                return new EnterprisePort(Bounds.X - Out, Clamp(PreferY, Bounds.Y + 28, Bounds.Bottom - 12), Side);
            default:
                return new EnterprisePort(Bounds.Right + Out, Clamp(PreferY, Bounds.Y + 28, Bounds.Bottom - 12), Side);
        }
    }

    internal static EnterprisePortSide FacingSide(EnterpriseRect From, double ToX, double ToY)
    {
        double Dx = ToX - From.CentreX;
        double Dy = ToY - From.CentreY;
        if (Math.Abs(Dy) >= Math.Abs(Dx))
        {
            return Dy >= 0 ? EnterprisePortSide.Bottom : EnterprisePortSide.Top;
        }

        return Dx >= 0 ? EnterprisePortSide.Right : EnterprisePortSide.Left;
    }

    internal static EnterprisePort PortOnNode(
        EnterpriseLayoutNode Node,
        EnterprisePortSide Side,
        double SlotT)
    {
        EnterpriseRect Bounds = EnterpriseObstacleCatalog.BoundsOf(Node);
        double T = Math.Clamp(SlotT, 0.18, 0.82);
        double TitleSkip = 0;
        if (Node.IsCompound && !Node.IsGroup)
        {
            TitleSkip = EnterpriseObstacleCatalog.COMPOUND_TITLE_HEIGHT;
        }
        else if (Node.IsGroup)
        {
            TitleSkip = EnterpriseObstacleCatalog.GROUP_TAB_HEIGHT;
        }

        switch (Side)
        {
            case EnterprisePortSide.Top:
                if (Node.IsGroup)
                {
                    EnterpriseRect? Tab = EnterpriseObstacleCatalog.GroupTabOf(Node);
                    double Left = Tab.HasValue ? Tab.Value.Right + 10 : Bounds.X + 10;
                    return new EnterprisePort(Lerp(Left, Bounds.Right - 10, T), Bounds.Y + TitleSkip, Side);
                }

                if (Node.IsCompound)
                {
                    return new EnterprisePort(Lerp(Bounds.X + 10, Bounds.Right - 10, T), Bounds.Y + TitleSkip, EnterprisePortSide.Top);
                }

                return new EnterprisePort(Lerp(Bounds.X + 10, Bounds.Right - 10, T), Bounds.Y, Side);
            case EnterprisePortSide.Bottom:
                return new EnterprisePort(Lerp(Bounds.X + 10, Bounds.Right - 10, T), Bounds.Bottom, Side);
            case EnterprisePortSide.Left:
                return new EnterprisePort(Bounds.X, Lerp(Bounds.Y + TitleSkip + 10, Bounds.Bottom - 10, T), Side);
            default:
                return new EnterprisePort(Bounds.Right, Lerp(Bounds.Y + TitleSkip + 10, Bounds.Bottom - 10, T), Side);
        }
    }

    private static List<(double X, double Y)> RouteHop(
        EnterprisePort From,
        EnterprisePort To,
        List<EnterpriseRect> Obstacles,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        HashSet<long> UsedCells)
    {
        List<(double X, double Y)> Stubbed = new List<(double X, double Y)>();
        (double FromX, double FromY) = Offset(From, STUB);
        (double ToX, double ToY) = Offset(To, STUB);
        Stubbed.Add((From.X, From.Y));
        List<(double X, double Y)>? Path = Search(FromX, FromY, ToX, ToY, Obstacles, Nodes, UsedCells);
        if (Path == null)
        {
            Path = FallbackBox(FromX, FromY, ToX, ToY, Obstacles);
        }

        Stubbed.AddRange(Path);
        Stubbed.Add((To.X, To.Y));
        return Stubbed;
    }

    private static List<(double X, double Y)>? Search(
        double FromX,
        double FromY,
        double ToX,
        double ToY,
        List<EnterpriseRect> Obstacles,
        Dictionary<string, EnterpriseLayoutNode> Nodes,
        HashSet<long> UsedCells)
    {
        double MinX = Math.Min(FromX, ToX);
        double MinY = Math.Min(FromY, ToY);
        double MaxX = Math.Max(FromX, ToX);
        double MaxY = Math.Max(FromY, ToY);
        foreach (EnterpriseLayoutNode Node in Nodes.Values)
        {
            EnterpriseRect Bounds = EnterpriseObstacleCatalog.BoundsOf(Node);
            MinX = Math.Min(MinX, Bounds.X);
            MinY = Math.Min(MinY, Bounds.Y);
            MaxX = Math.Max(MaxX, Bounds.Right);
            MaxY = Math.Max(MaxY, Bounds.Bottom);
        }

        MinX -= WORLD_MARGIN;
        MinY -= WORLD_MARGIN;
        MaxX += WORLD_MARGIN;
        MaxY += WORLD_MARGIN;
        int Columns = Math.Max(4, (int)Math.Ceiling((MaxX - MinX) / CELL_SIZE));
        int Rows = Math.Max(4, (int)Math.Ceiling((MaxY - MinY) / CELL_SIZE));
        int Start = CellIndex(FromX, FromY, MinX, MinY, Columns, Rows);
        int Goal = CellIndex(ToX, ToY, MinX, MinY, Columns, Rows);
        bool[] Blocked = BuildBlocked(Columns, Rows, MinX, MinY, Obstacles, FromX, FromY, ToX, ToY);
        int[] CameFrom = new int[Columns * Rows];
        int[] GScore = new int[Columns * Rows];
        int[] IncomingDir = new int[Columns * Rows];
        for (int Index = 0; Index < GScore.Length; Index++)
        {
            CameFrom[Index] = -1;
            GScore[Index] = int.MaxValue;
            IncomingDir[Index] = -1;
        }

        GScore[Start] = 0;
        List<int> Open = new List<int> { Start };
        int[] DeltaX = [1, -1, 0, 0];
        int[] DeltaY = [0, 0, 1, -1];
        while (Open.Count > 0)
        {
            int BestOpen = 0;
            int BestF = int.MaxValue;
            for (int OpenIndex = 0; OpenIndex < Open.Count; OpenIndex++)
            {
                int Candidate = Open[OpenIndex];
                int Heuristic = Manhattan(Candidate, Goal, Columns);
                int F = GScore[Candidate] + Heuristic;
                if (F < BestF)
                {
                    BestF = F;
                    BestOpen = OpenIndex;
                }
            }

            int Current = Open[BestOpen];
            Open.RemoveAt(BestOpen);
            if (Current == Goal)
            {
                return Reconstruct(Current, CameFrom, MinX, MinY, Columns, FromX, FromY, ToX, ToY);
            }

            int CurrentColumn = Current % Columns;
            int CurrentRow = Current / Columns;
            for (int Direction = 0; Direction < 4; Direction++)
            {
                int NextColumn = CurrentColumn + DeltaX[Direction];
                int NextRow = CurrentRow + DeltaY[Direction];
                if (NextColumn < 0 || NextColumn >= Columns || NextRow < 0 || NextRow >= Rows)
                {
                    continue;
                }

                int Next = NextRow * Columns + NextColumn;
                if (Blocked[Next] && Next != Goal)
                {
                    continue;
                }

                int Step = 10;
                if (IncomingDir[Current] >= 0 && IncomingDir[Current] != Direction)
                {
                    Step += TURN_COST;
                }

                if (UsedCells.Contains(PackCell(NextColumn, NextRow)))
                {
                    Step += USED_CELL_COST;
                }

                int Tentative = GScore[Current] + Step;
                if (Tentative >= GScore[Next])
                {
                    continue;
                }

                CameFrom[Next] = Current;
                GScore[Next] = Tentative;
                IncomingDir[Next] = Direction;
                if (!Open.Contains(Next))
                {
                    Open.Add(Next);
                }
            }
        }

        return null;
    }

    private static bool[] BuildBlocked(
        int Columns,
        int Rows,
        double MinX,
        double MinY,
        List<EnterpriseRect> Obstacles,
        double FromX,
        double FromY,
        double ToX,
        double ToY)
    {
        bool[] Blocked = new bool[Columns * Rows];
        for (int Row = 0; Row < Rows; Row++)
        {
            for (int Column = 0; Column < Columns; Column++)
            {
                double X = MinX + ((Column + 0.5) * CELL_SIZE);
                double Y = MinY + ((Row + 0.5) * CELL_SIZE);
                if (Distance(X, Y, FromX, FromY) <= CELL_SIZE * 1.2 || Distance(X, Y, ToX, ToY) <= CELL_SIZE * 1.2)
                {
                    continue;
                }

                for (int ObstacleIndex = 0; ObstacleIndex < Obstacles.Count; ObstacleIndex++)
                {
                    if (Obstacles[ObstacleIndex].Contains(X, Y, -1))
                    {
                        Blocked[Row * Columns + Column] = true;
                        break;
                    }
                }
            }
        }

        return Blocked;
    }

    private static List<(double X, double Y)> Reconstruct(
        int Goal,
        int[] CameFrom,
        double MinX,
        double MinY,
        int Columns,
        double FromX,
        double FromY,
        double ToX,
        double ToY)
    {
        List<(double X, double Y)> Reverse = new List<(double X, double Y)>();
        int Current = Goal;
        while (Current >= 0)
        {
            int Column = Current % Columns;
            int Row = Current / Columns;
            Reverse.Add((MinX + ((Column + 0.5) * CELL_SIZE), MinY + ((Row + 0.5) * CELL_SIZE)));
            Current = CameFrom[Current];
        }

        Reverse.Reverse();
        if (Reverse.Count == 0)
        {
            Reverse.Add((FromX, FromY));
            Reverse.Add((ToX, ToY));
            return Reverse;
        }

        Reverse[0] = (FromX, FromY);
        Reverse[Reverse.Count - 1] = (ToX, ToY);
        return Reverse;
    }

    private static List<(double X, double Y)> FallbackBox(
        double FromX,
        double FromY,
        double ToX,
        double ToY,
        List<EnterpriseRect> Obstacles)
    {
        double Left = Math.Min(FromX, ToX) - 40;
        double Right = Math.Max(FromX, ToX) + 40;
        foreach (EnterpriseRect Obstacle in Obstacles)
        {
            Left = Math.Min(Left, Obstacle.X - 16);
            Right = Math.Max(Right, Obstacle.Right + 16);
        }

        double ViaX = Math.Abs(FromX - Left) <= Math.Abs(FromX - Right) ? Left : Right;
        return new List<(double X, double Y)>
        {
            (FromX, FromY),
            (ViaX, FromY),
            (ViaX, ToY),
            (ToX, ToY)
        };
    }

    private static void AppendHop(List<(double X, double Y)> Combined, List<(double X, double Y)> Hop)
    {
        int Start = Combined.Count == 0 ? 0 : 1;
        for (int Index = Start; Index < Hop.Count; Index++)
        {
            Combined.Add(Hop[Index]);
        }
    }

    private static List<(double X, double Y)> Simplify(List<(double X, double Y)> Points)
    {
        if (Points.Count < 3)
        {
            return Points;
        }

        List<(double X, double Y)> Result = new List<(double X, double Y)>();
        Result.Add(Points[0]);
        for (int Index = 1; Index < Points.Count - 1; Index++)
        {
            double Ax = Result[Result.Count - 1].X;
            double Ay = Result[Result.Count - 1].Y;
            double Bx = Points[Index].X;
            double By = Points[Index].Y;
            double Cx = Points[Index + 1].X;
            double Cy = Points[Index + 1].Y;
            bool Colinear = (Math.Abs(Ax - Bx) < 1 && Math.Abs(Bx - Cx) < 1)
                || (Math.Abs(Ay - By) < 1 && Math.Abs(By - Cy) < 1);
            if (!Colinear)
            {
                Result.Add(Points[Index]);
            }
        }

        Result.Add(Points[Points.Count - 1]);
        return Result;
    }

    private static void MarkUsed(List<(double X, double Y)> Points, HashSet<long> UsedCells)
    {
        for (int Index = 0; Index < Points.Count; Index++)
        {
            int Column = (int)Math.Round(Points[Index].X / CELL_SIZE);
            int Row = (int)Math.Round(Points[Index].Y / CELL_SIZE);
            UsedCells.Add(PackCell(Column, Row));
        }
    }

    private static (double X, double Y) Offset(EnterprisePort Port, double Distance)
    {
        switch (Port.Side)
        {
            case EnterprisePortSide.Top:
                return (Port.X, Port.Y - Distance);
            case EnterprisePortSide.Bottom:
                return (Port.X, Port.Y + Distance);
            case EnterprisePortSide.Left:
                return (Port.X - Distance, Port.Y);
            default:
                return (Port.X + Distance, Port.Y);
        }
    }

    private static int CellIndex(double X, double Y, double MinX, double MinY, int Columns, int Rows)
    {
        int Column = (int)Math.Clamp(Math.Floor((X - MinX) / CELL_SIZE), 0, Columns - 1);
        int Row = (int)Math.Clamp(Math.Floor((Y - MinY) / CELL_SIZE), 0, Rows - 1);
        return Row * Columns + Column;
    }

    private static int Manhattan(int From, int Goal, int Columns)
    {
        int FromColumn = From % Columns;
        int FromRow = From / Columns;
        int GoalColumn = Goal % Columns;
        int GoalRow = Goal / Columns;
        return (Math.Abs(FromColumn - GoalColumn) + Math.Abs(FromRow - GoalRow)) * 10;
    }

    private static long PackCell(int Column, int Row)
    {
        return ((long)Column << 32) ^ (uint)Row;
    }

    private static double Distance(double X1, double Y1, double X2, double Y2)
    {
        double Dx = X2 - X1;
        double Dy = Y2 - Y1;
        return Math.Sqrt((Dx * Dx) + (Dy * Dy));
    }

    private static double Lerp(double A, double B, double T)
    {
        return A + ((B - A) * T);
    }

    private static double Clamp(double Value, double Min, double Max)
    {
        if (Max < Min)
        {
            return (Min + Max) / 2.0;
        }

        return Math.Clamp(Value, Min, Max);
    }
}
