using System;
using System.Collections.Generic;
using System.Text;

using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

/// <summary>
/// Low-bend orthogonal corridor router. One style for a whole diagram: straight
/// stubs, shared channels, rounded corners, and a four-bend budget.
/// </summary>
internal static class EnterpriseOrthogonalRouter
{
    internal const double BORDER_GAP = 10;

    internal const double CHANNEL_GAP = 8;

    internal const double STUB = 12;

    internal const double CORNER_RADIUS = 7;

    internal const int MAX_BENDS = 4;

    internal static List<List<(double X, double Y)>> RouteAll(
        List<EnterpriseRouteWork> Work,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<List<(double X, double Y)>> Paths = new List<List<(double X, double Y)>>();
        foreach (EnterpriseRouteWork Item in Work)
        {
            Paths.Add(RouteOne(Item, Nodes));
        }

        for (int Index = 0; Index < Paths.Count; Index++)
        {
            Paths[Index] = EnsureOutwardStubs(
                Simplify(Paths[Index]),
                Work[Index].SourcePort,
                Work[Index].TargetPort);
        }

        AssignChannels(Paths);
        for (int Index = 0; Index < Paths.Count; Index++)
        {
            Paths[Index] = ClampToPorts(Simplify(Paths[Index]), Work[Index].SourcePort, Work[Index].TargetPort);
        }

        return Paths;
    }

    internal static string BuildRoundedPathData(List<(double X, double Y)> Points)
    {
        List<(double X, double Y)> Clean = Simplify(Points);
        if (Clean.Count < 2)
        {
            return string.Empty;
        }

        StringBuilder Builder = new StringBuilder();
        Builder.Append('M')
            .Append(EnterpriseSvgFormat.Number(Clean[0].X))
            .Append(',')
            .Append(EnterpriseSvgFormat.Number(Clean[0].Y));
        for (int Index = 1; Index < Clean.Count; Index++)
        {
            if (Index < Clean.Count - 1)
            {
                double IncomingX = Clean[Index].X - Clean[Index - 1].X;
                double IncomingY = Clean[Index].Y - Clean[Index - 1].Y;
                double OutgoingX = Clean[Index + 1].X - Clean[Index].X;
                double OutgoingY = Clean[Index + 1].Y - Clean[Index].Y;
                double InLen = Math.Abs(IncomingX) + Math.Abs(IncomingY);
                double OutLen = Math.Abs(OutgoingX) + Math.Abs(OutgoingY);
                double Radius = Math.Min(CORNER_RADIUS, Math.Min(InLen, OutLen) / 2.2);
                if (Radius >= 2)
                {
                    double Ix = IncomingX == 0 ? 0 : Math.Sign(IncomingX);
                    double Iy = IncomingY == 0 ? 0 : Math.Sign(IncomingY);
                    double Ox = OutgoingX == 0 ? 0 : Math.Sign(OutgoingX);
                    double Oy = OutgoingY == 0 ? 0 : Math.Sign(OutgoingY);
                    Builder.Append('L')
                        .Append(EnterpriseSvgFormat.Number(Clean[Index].X - (Ix * Radius)))
                        .Append(',')
                        .Append(EnterpriseSvgFormat.Number(Clean[Index].Y - (Iy * Radius)))
                        .Append('Q')
                        .Append(EnterpriseSvgFormat.Number(Clean[Index].X))
                        .Append(',')
                        .Append(EnterpriseSvgFormat.Number(Clean[Index].Y))
                        .Append(' ')
                        .Append(EnterpriseSvgFormat.Number(Clean[Index].X + (Ox * Radius)))
                        .Append(',')
                        .Append(EnterpriseSvgFormat.Number(Clean[Index].Y + (Oy * Radius)));
                    continue;
                }
            }

            Builder.Append('L')
                .Append(EnterpriseSvgFormat.Number(Clean[Index].X))
                .Append(',')
                .Append(EnterpriseSvgFormat.Number(Clean[Index].Y));
        }

        return Builder.ToString();
    }

    internal static (double X, double Y) LabelAnchor(List<(double X, double Y)> Points)
    {
        List<(double X, double Y)> Clean = Simplify(Points);
        if (Clean.Count < 2)
        {
            return (0, 0);
        }

        int Best = 0;
        double BestLength = -1;
        for (int Index = 0; Index < Clean.Count - 1; Index++)
        {
            double Length = Math.Abs(Clean[Index + 1].X - Clean[Index].X)
                + Math.Abs(Clean[Index + 1].Y - Clean[Index].Y);
            if (Length > BestLength)
            {
                BestLength = Length;
                Best = Index;
            }
        }

        return (
            (Clean[Best].X + Clean[Best + 1].X) / 2.0,
            (Clean[Best].Y + Clean[Best + 1].Y) / 2.0);
    }

    internal static int CountBends(List<(double X, double Y)> Points)
    {
        List<(double X, double Y)> Clean = Simplify(Points);
        int Bends = 0;
        for (int Index = 1; Index < Clean.Count - 1; Index++)
        {
            double Ax = Clean[Index].X - Clean[Index - 1].X;
            double Ay = Clean[Index].Y - Clean[Index - 1].Y;
            double Bx = Clean[Index + 1].X - Clean[Index].X;
            double By = Clean[Index + 1].Y - Clean[Index].Y;
            if (Math.Abs(Math.Sign(Ax) - Math.Sign(Bx)) + Math.Abs(Math.Sign(Ay) - Math.Sign(By)) > 0
                && (Math.Abs(Ax) + Math.Abs(Ay) > 0.5)
                && (Math.Abs(Bx) + Math.Abs(By) > 0.5))
            {
                Bends++;
            }
        }

        return Bends;
    }

    internal static bool CrossesContainer(
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        return EnterpriseObstacleCatalog.CrossingContainers(Source, Target, Nodes).Count > 0
            || EnterpriseObstacleCatalog.CrossingContainers(Target, Source, Nodes).Count > 0;
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

    internal static EnterprisePortSide PreferredExitSide(
        EnterpriseLayoutNode From,
        EnterpriseLayoutNode Toward,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        EnterprisePortSide Side = FacingSide(
            EnterpriseObstacleCatalog.BoundsOf(From),
            EnterpriseObstacleCatalog.BoundsOf(Toward).CentreX,
            EnterpriseObstacleCatalog.BoundsOf(Toward).CentreY);
        if (!string.IsNullOrEmpty(From.ParentId)
            && Nodes.TryGetValue(From.ParentId, out EnterpriseLayoutNode? Parent)
            && Parent.IsCompound
            && !Parent.IsGroup
            && !EnterpriseObstacleCatalog.IsAncestor(Parent, Toward, Nodes)
            && !string.Equals(Parent.Id, Toward.Id, StringComparison.Ordinal)
            && Side == EnterprisePortSide.Top)
        {
            return EnterpriseObstacleCatalog.BoundsOf(Toward).CentreX < EnterpriseObstacleCatalog.BoundsOf(From).CentreX
                ? EnterprisePortSide.Left
                : EnterprisePortSide.Right;
        }

        return Side;
    }

    internal static EnterprisePort PortOnNode(
        EnterpriseLayoutNode Node,
        EnterprisePortSide Side,
        double SlotT)
    {
        EnterpriseRect Bounds = EnterpriseObstacleCatalog.BoundsOf(Node);
        double T = Math.Clamp(SlotT, 0.22, 0.78);
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
                    double Left = Tab.HasValue ? Tab.Value.Right + 16 : Bounds.X + 16;
                    return new EnterprisePort(Lerp(Left, Bounds.Right - 16, T), Bounds.Y + TitleSkip, Side);
                }

                if (Node.IsCompound)
                {
                    return new EnterprisePort(
                        Lerp(Bounds.X + 12, Bounds.Right - 12, T),
                        Bounds.Y + TitleSkip,
                        EnterprisePortSide.Top);
                }

                return new EnterprisePort(Lerp(Bounds.X + 12, Bounds.Right - 12, T), Bounds.Y, Side);
            case EnterprisePortSide.Bottom:
                return new EnterprisePort(Lerp(Bounds.X + 12, Bounds.Right - 12, T), Bounds.Bottom, Side);
            case EnterprisePortSide.Left:
                return new EnterprisePort(Bounds.X, Lerp(Bounds.Y + TitleSkip + 12, Bounds.Bottom - 12, T), Side);
            default:
                return new EnterprisePort(Bounds.Right, Lerp(Bounds.Y + TitleSkip + 12, Bounds.Bottom - 12, T), Side);
        }
    }

    private static List<(double X, double Y)> RouteOne(
        EnterpriseRouteWork Work,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        HashSet<string> AllClosed = new HashSet<string>(StringComparer.Ordinal);
        foreach (EnterpriseLayoutNode Container in EnterpriseObstacleCatalog.CrossingContainers(Work.Source, Work.Target, Nodes))
        {
            AllClosed.Add(Container.Id);
        }

        foreach (EnterpriseLayoutNode Container in EnterpriseObstacleCatalog.CrossingContainers(Work.Target, Work.Source, Nodes))
        {
            AllClosed.Add(Container.Id);
        }

        List<EnterpriseRect> DirectObstacles = EnterpriseObstacleCatalog.ObstaclesForHop(
            Work.Source,
            Work.Target,
            Nodes,
            AllClosed,
            true);
        List<(double X, double Y)> Direct = RouteHop(Work.SourcePort, Work.TargetPort, DirectObstacles, Nodes);
        if (Direct.Count >= 2 && CountBends(Direct) <= MAX_BENDS && Clears(Direct, DirectObstacles, Nodes))
        {
            return ClampToPorts(Direct, Work.SourcePort, Work.TargetPort);
        }

        List<EnterprisePort> Waypoints = BuildWaypoints(Work.Source, Work.Target, Work.SourcePort, Work.TargetPort, Nodes);
        List<(double X, double Y)> Combined = new List<(double X, double Y)>();
        HashSet<string> Closed = new HashSet<string>(StringComparer.Ordinal);
        List<EnterpriseLayoutNode> Exits = EnterpriseObstacleCatalog.CrossingContainers(Work.Source, Work.Target, Nodes);
        for (int Index = 0; Index < Waypoints.Count - 1; Index++)
        {
            List<EnterpriseRect> Obstacles = EnterpriseObstacleCatalog.ObstaclesForHop(
                Work.Source,
                Work.Target,
                Nodes,
                Closed,
                true);
            List<(double X, double Y)> Hop = RouteHop(Waypoints[Index], Waypoints[Index + 1], Obstacles, Nodes);
            AppendHop(Combined, Hop);
            if (Index < Exits.Count)
            {
                Closed.Add(Exits[Index].Id);
            }
        }

        if (Combined.Count < 2)
        {
            Combined.Add((Work.SourcePort.X, Work.SourcePort.Y));
            Combined.Add((Work.TargetPort.X, Work.TargetPort.Y));
        }

        Combined = ClampToPorts(Simplify(Combined), Work.SourcePort, Work.TargetPort);
        if (CountBends(Combined) > MAX_BENDS)
        {
            List<EnterpriseRect> InteriorObstacles = EnterpriseObstacleCatalog.ObstaclesForHop(
                Work.Source,
                Work.Target,
                Nodes,
                new HashSet<string>(StringComparer.Ordinal),
                true);
            List<(double X, double Y)> Interior = RouteHop(Work.SourcePort, Work.TargetPort, InteriorObstacles, Nodes);
            if (Interior.Count >= 2
                && CountBends(Interior) <= MAX_BENDS
                && Clears(Interior, InteriorObstacles, Nodes))
            {
                return ClampToPorts(Interior, Work.SourcePort, Work.TargetPort);
            }
        }

        return Combined;
    }

    private static List<EnterprisePort> BuildWaypoints(
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        EnterprisePort SourcePort,
        EnterprisePort TargetPort,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        List<EnterprisePort> Waypoints = new List<EnterprisePort> { SourcePort };
        foreach (EnterpriseLayoutNode Container in EnterpriseObstacleCatalog.CrossingContainers(Source, Target, Nodes))
        {
            Waypoints.Add(ChooseGate(Container, Source, Target, true, Nodes));
        }

        List<EnterpriseLayoutNode> Entries = EnterpriseObstacleCatalog.CrossingContainers(Target, Source, Nodes);
        Entries.Reverse();
        foreach (EnterpriseLayoutNode Container in Entries)
        {
            Waypoints.Add(ChooseGate(Container, Target, Source, false, Nodes));
        }

        Waypoints.Add(TargetPort);
        return Waypoints;
    }

    private static EnterprisePort ChooseGate(
        EnterpriseLayoutNode Container,
        EnterpriseLayoutNode Inner,
        EnterpriseLayoutNode Outer,
        bool Exiting,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        EnterpriseRect Bounds = EnterpriseObstacleCatalog.BoundsOf(Container);
        EnterpriseRect InnerBounds = EnterpriseObstacleCatalog.BoundsOf(Inner);
        EnterprisePortSide Side = PreferredExitSide(Container, Outer, Nodes);
        if (Container.IsCompound && !Container.IsGroup && Side == EnterprisePortSide.Top)
        {
            Side = Outer.AbsoluteX + (Outer.Width / 2.0) < Bounds.CentreX
                ? EnterprisePortSide.Left
                : EnterprisePortSide.Right;
        }

        if (Container.IsGroup && Side == EnterprisePortSide.Top)
        {
            EnterpriseRect? Tab = EnterpriseObstacleCatalog.GroupTabOf(Container);
            double Left = Tab.HasValue ? Tab.Value.Right + 16 : Bounds.X + 16;
            double X = Clamp(InnerBounds.CentreX, Left, Bounds.Right - 16);
            return new EnterprisePort(X, Bounds.Y + EnterpriseObstacleCatalog.GROUP_TAB_HEIGHT, EnterprisePortSide.Top);
        }

        switch (Side)
        {
            case EnterprisePortSide.Top:
                return new EnterprisePort(Clamp(InnerBounds.CentreX, Bounds.X + 16, Bounds.Right - 16), Bounds.Y, Side);
            case EnterprisePortSide.Bottom:
                return new EnterprisePort(Clamp(InnerBounds.CentreX, Bounds.X + 16, Bounds.Right - 16), Bounds.Bottom, Side);
            case EnterprisePortSide.Left:
                return new EnterprisePort(Bounds.X, Clamp(InnerBounds.CentreY, Bounds.Y + 28, Bounds.Bottom - 16), Side);
            default:
                return new EnterprisePort(Bounds.Right, Clamp(InnerBounds.CentreY, Bounds.Y + 28, Bounds.Bottom - 16), Side);
        }
    }

    private static List<(double X, double Y)> RouteHop(
        EnterprisePort From,
        EnterprisePort To,
        List<EnterpriseRect> Obstacles,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        (double FromX, double FromY) = Offset(From, STUB);
        (double ToX, double ToY) = Offset(To, STUB);
        List<List<(double X, double Y)>> Candidates = BuildCandidates(
            From.X,
            From.Y,
            FromX,
            FromY,
            ToX,
            ToY,
            To.X,
            To.Y,
            Obstacles);
        List<(double X, double Y)>? Best = null;
        int BestScore = int.MaxValue;
        foreach (List<(double X, double Y)> Candidate in Candidates)
        {
            List<(double X, double Y)> Clean = Simplify(Candidate);
            int Bends = CountBends(Clean);
            if (Bends > MAX_BENDS || !Clears(Clean, Obstacles, Nodes))
            {
                continue;
            }

            int Score = (Bends * 10000) + (int)PathLength(Clean);
            if (Score < BestScore)
            {
                BestScore = Score;
                Best = Clean;
            }
        }

        if (Best != null)
        {
            return Best;
        }

        List<(double X, double Y)> Fallback = new List<(double X, double Y)>
        {
            (From.X, From.Y),
            (FromX, FromY),
            (FromX, ToY),
            (ToX, ToY),
            (To.X, To.Y)
        };
        List<(double X, double Y)> CleanFallback = Simplify(Fallback);
        if (Clears(CleanFallback, Obstacles, Nodes))
        {
            return CleanFallback;
        }

        return Best ?? CleanFallback;
    }

    private static List<List<(double X, double Y)>> BuildCandidates(
        double Ax,
        double Ay,
        double Sx,
        double Sy,
        double Tx,
        double Ty,
        double Bx,
        double By,
        List<EnterpriseRect> Obstacles)
    {
        List<List<(double X, double Y)>> Candidates = new List<List<(double X, double Y)>>();
        AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Tx, Ty), (Bx, By) });
        AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Sx, Ty), (Tx, Ty), (Bx, By) });
        AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Tx, Sy), (Tx, Ty), (Bx, By) });
        double MidY = (Sy + Ty) / 2.0;
        AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Sx, MidY), (Tx, MidY), (Tx, Ty), (Bx, By) });
        double MidX = (Sx + Tx) / 2.0;
        AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (MidX, Sy), (MidX, Ty), (Tx, Ty), (Bx, By) });
        double[] Spreads = { 36, 56, 80, 110 };
        foreach (double Spread in Spreads)
        {
            double Left = Math.Min(Sx, Tx) - Spread;
            double Right = Math.Max(Sx, Tx) + Spread;
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Left, Sy), (Left, Ty), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Right, Sy), (Right, Ty), (Tx, Ty), (Bx, By) });
            double Top = Math.Min(Sy, Ty) - Spread;
            double Bottom = Math.Max(Sy, Ty) + Spread;
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Sx, Top), (Tx, Top), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Sx, Bottom), (Tx, Bottom), (Tx, Ty), (Bx, By) });
        }
        if (TryUnion(Obstacles, out EnterpriseRect Union))
        {
            double AroundLeft = Union.X - BORDER_GAP - STUB;
            double AroundRight = Union.Right + BORDER_GAP + STUB;
            double AroundTop = Union.Y - BORDER_GAP - STUB;
            double AroundBottom = Union.Bottom + BORDER_GAP + STUB;
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (AroundLeft, Sy), (AroundLeft, Ty), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (AroundRight, Sy), (AroundRight, Ty), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Sx, AroundTop), (Tx, AroundTop), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (Sx, AroundBottom), (Tx, AroundBottom), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (AroundLeft, Sy), (AroundLeft, AroundTop), (Tx, AroundTop), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (AroundRight, Sy), (AroundRight, AroundTop), (Tx, AroundTop), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (AroundLeft, Sy), (AroundLeft, AroundBottom), (Tx, AroundBottom), (Tx, Ty), (Bx, By) });
            AddCandidate(Candidates, new List<(double X, double Y)> { (Ax, Ay), (Sx, Sy), (AroundRight, Sy), (AroundRight, AroundBottom), (Tx, AroundBottom), (Tx, Ty), (Bx, By) });
        }

        return Candidates;
    }

    private static bool TryUnion(List<EnterpriseRect> Obstacles, out EnterpriseRect Union)
    {
        if (Obstacles.Count == 0)
        {
            Union = new EnterpriseRect(0, 0, 0, 0);
            return false;
        }

        double Left = double.MaxValue;
        double Top = double.MaxValue;
        double Right = double.MinValue;
        double Bottom = double.MinValue;
        foreach (EnterpriseRect Obstacle in Obstacles)
        {
            Left = Math.Min(Left, Obstacle.X);
            Top = Math.Min(Top, Obstacle.Y);
            Right = Math.Max(Right, Obstacle.Right);
            Bottom = Math.Max(Bottom, Obstacle.Bottom);
        }

        Union = new EnterpriseRect(Left, Top, Right - Left, Bottom - Top);
        return Union.Width > 0 && Union.Height > 0;
    }

    private static void AddCandidate(List<List<(double X, double Y)>> Candidates, List<(double X, double Y)> Path)
    {
        Candidates.Add(Path);
    }

    private static bool Clears(
        List<(double X, double Y)> Points,
        List<EnterpriseRect> Obstacles,
        Dictionary<string, EnterpriseLayoutNode> Nodes)
    {
        if (Points.Count < 2)
        {
            return false;
        }

        for (int Index = 0; Index < Points.Count - 1; Index++)
        {
            (double X1, double Y1) = Points[Index];
            (double X2, double Y2) = Points[Index + 1];
            bool AttachStub = (Index == 0 || Index == Points.Count - 2)
                && SegmentLength(X1, Y1, X2, Y2) <= STUB + 2;
            foreach (EnterpriseRect Obstacle in Obstacles)
            {
                if (Obstacle.IntersectsSegment(X1, Y1, X2, Y2, 0.8))
                {
                    if (AttachStub)
                    {
                        continue;
                    }

                    return false;
                }
            }

            if (!AttachStub)
            {
                foreach (EnterpriseLayoutNode Node in Nodes.Values)
                {
                    EnterpriseRect Bounds = EnterpriseObstacleCatalog.BoundsOf(Node);
                    if (Bounds.IsAlongBorder(X1, Y1, X2, Y2, BORDER_GAP, 16))
                    {
                        return false;
                    }

                    EnterpriseRect? Title = EnterpriseObstacleCatalog.TitleBandOf(Node);
                    if (Title.HasValue && Title.Value.IntersectsSegment(X1, Y1, X2, Y2, 0.5))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static void AssignChannels(List<List<(double X, double Y)>> Paths)
    {
        for (int Pass = 0; Pass < 4; Pass++)
        {
            bool Moved = false;
            for (int Left = 0; Left < Paths.Count; Left++)
            {
                for (int Right = Left + 1; Right < Paths.Count; Right++)
                {
                    Moved = OffsetIfOverlap(Paths[Left], Paths[Right]) || Moved;
                }
            }

            if (!Moved)
            {
                return;
            }
        }
    }

    private static bool OffsetIfOverlap(List<(double X, double Y)> Left, List<(double X, double Y)> Right)
    {
        bool Moved = false;
        for (int Li = 1; Li < Left.Count - 2; Li++)
        {
            for (int Ri = 1; Ri < Right.Count - 2; Ri++)
            {
                (double Ax, double Ay) = Left[Li];
                (double Bx, double By) = Left[Li + 1];
                (double Cx, double Cy) = Right[Ri];
                (double Dx, double Dy) = Right[Ri + 1];
                bool LeftVertical = Math.Abs(Ax - Bx) < 0.6;
                bool RightVertical = Math.Abs(Cx - Dx) < 0.6;
                if (LeftVertical && RightVertical && RangesOverlap(Ay, By, Cy, Dy, 8))
                {
                    double Gap = Math.Abs(Ax - Cx);
                    if (Gap < CHANNEL_GAP)
                    {
                        double Shift = Gap < 0.01 ? CHANNEL_GAP : CHANNEL_GAP - Gap;
                        ShiftSegmentX(Right, Ri, Cx <= Ax ? -Shift : Shift);
                        Moved = true;
                    }
                }

                bool LeftHorizontal = Math.Abs(Ay - By) < 0.6;
                bool RightHorizontal = Math.Abs(Cy - Dy) < 0.6;
                if (LeftHorizontal && RightHorizontal && RangesOverlap(Ax, Bx, Cx, Dx, 8))
                {
                    double Gap = Math.Abs(Ay - Cy);
                    if (Gap < CHANNEL_GAP)
                    {
                        double Shift = Gap < 0.01 ? CHANNEL_GAP : CHANNEL_GAP - Gap;
                        ShiftSegmentY(Right, Ri, Cy <= Ay ? -Shift : Shift);
                        Moved = true;
                    }
                }
            }
        }

        return Moved;
    }

    private static void ShiftSegmentX(List<(double X, double Y)> Points, int Index, double Shift)
    {
        Points[Index] = (Points[Index].X + Shift, Points[Index].Y);
        Points[Index + 1] = (Points[Index + 1].X + Shift, Points[Index + 1].Y);
    }

    private static void ShiftSegmentY(List<(double X, double Y)> Points, int Index, double Shift)
    {
        Points[Index] = (Points[Index].X, Points[Index].Y + Shift);
        Points[Index + 1] = (Points[Index + 1].X, Points[Index + 1].Y + Shift);
    }

    private static bool RangesOverlap(double A1, double A2, double B1, double B2, double MinOverlap)
    {
        double Low = Math.Max(Math.Min(A1, A2), Math.Min(B1, B2));
        double High = Math.Min(Math.Max(A1, A2), Math.Max(B1, B2));
        return High - Low >= MinOverlap;
    }

    private static List<(double X, double Y)> ClampToPorts(
        List<(double X, double Y)> Points,
        EnterprisePort Source,
        EnterprisePort Target)
    {
        if (Points.Count == 0)
        {
            return new List<(double X, double Y)> { (Source.X, Source.Y), (Target.X, Target.Y) };
        }

        Points[0] = (Source.X, Source.Y);
        Points[Points.Count - 1] = (Target.X, Target.Y);
        return EnsureOutwardStubs(Points, Source, Target);
    }

    private static List<(double X, double Y)> EnsureOutwardStubs(
        List<(double X, double Y)> Points,
        EnterprisePort Source,
        EnterprisePort Target)
    {
        (double Sx, double Sy) = Offset(Source, STUB);
        (double Tx, double Ty) = Offset(Target, STUB);
        List<(double X, double Y)> Result = new List<(double X, double Y)>();
        Result.Add((Source.X, Source.Y));
        if (Points.Count < 2 || !SamePoint(Points[1], Sx, Sy))
        {
            Result.Add((Sx, Sy));
        }

        int Start = 1;
        int End = Points.Count - 1;
        if (Points.Count >= 2 && SamePoint(Points[0], Source.X, Source.Y))
        {
            Start = 1;
        }

        if (Points.Count >= 2 && SamePoint(Points[Points.Count - 1], Target.X, Target.Y))
        {
            End = Points.Count - 1;
        }

        for (int Index = Start; Index < End; Index++)
        {
            if (SamePoint(Points[Index], Source.X, Source.Y)
                || SamePoint(Points[Index], Target.X, Target.Y)
                || SamePoint(Points[Index], Sx, Sy)
                || SamePoint(Points[Index], Tx, Ty))
            {
                continue;
            }

            Result.Add(Points[Index]);
        }

        if (Result.Count < 2 || !SamePoint(Result[Result.Count - 1], Tx, Ty))
        {
            Result.Add((Tx, Ty));
        }

        Result.Add((Target.X, Target.Y));
        return Simplify(Result);
    }

    private static bool SamePoint((double X, double Y) Point, double X, double Y)
    {
        return Math.Abs(Point.X - X) < 0.6 && Math.Abs(Point.Y - Y) < 0.6;
    }

    private static void AppendHop(List<(double X, double Y)> Combined, List<(double X, double Y)> Hop)
    {
        int Start = Combined.Count == 0 ? 0 : 1;
        for (int Index = Start; Index < Hop.Count; Index++)
        {
            Combined.Add(Hop[Index]);
        }
    }

    internal static List<(double X, double Y)> Simplify(List<(double X, double Y)> Points)
    {
        if (Points.Count < 3)
        {
            return new List<(double X, double Y)>(Points);
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
            bool NearEnd = SegmentLength(Bx, By, Points[0].X, Points[0].Y) <= STUB + 2
                || SegmentLength(Bx, By, Points[Points.Count - 1].X, Points[Points.Count - 1].Y) <= STUB + 2;
            if ((!Colinear || NearEnd) && (Math.Abs(Ax - Bx) + Math.Abs(Ay - By) > 0.8))
            {
                Result.Add(Points[Index]);
            }
        }

        Result.Add(Points[Points.Count - 1]);
        return Result;
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

    private static double PathLength(List<(double X, double Y)> Points)
    {
        double Length = 0;
        for (int Index = 1; Index < Points.Count; Index++)
        {
            Length += SegmentLength(Points[Index - 1].X, Points[Index - 1].Y, Points[Index].X, Points[Index].Y);
        }

        return Length;
    }

    private static double SegmentLength(double X1, double Y1, double X2, double Y2)
    {
        return Math.Abs(X2 - X1) + Math.Abs(Y2 - Y1);
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

/// <summary>
/// One visible relationship ready for orthogonal routing.
/// </summary>
internal sealed class EnterpriseRouteWork
{
    internal EnterpriseRouteWork(
        EnterpriseLayoutNode Source,
        EnterpriseLayoutNode Target,
        EnterprisePort SourcePort,
        EnterprisePort TargetPort)
    {
        this.Source = Source;
        this.Target = Target;
        this.SourcePort = SourcePort;
        this.TargetPort = TargetPort;
    }

    internal EnterpriseLayoutNode Source { get; }

    internal EnterpriseLayoutNode Target { get; }

    internal EnterprisePort SourcePort { get; }

    internal EnterprisePort TargetPort { get; }
}
