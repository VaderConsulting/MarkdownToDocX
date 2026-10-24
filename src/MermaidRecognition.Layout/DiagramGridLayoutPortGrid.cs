using System;
using System.Collections.Generic;
using System.Linq;

using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

/// <summary>Places the lines of one node side on its centre-out connection-point grid (rule 3.2).</summary>
public static partial class DiagramGridLayout
{
    /// <summary>Largest difference between two grid coordinates that still counts as a straight run, in pixels.</summary>
    private const double GRID_ALIGNMENT_TOLERANCE_PX = 0.5;

    /// <summary>
    /// Chooses a grid point for every line on one side. A line whose peer faces this side across a shared grid
    /// coordinate takes that point so the connection can run straight; reserved straight ends keep the centre;
    /// the other lines fill the remaining points from the centre outwards in side order.
    /// </summary>
    /// <param name="Group">Lines sharing the side, in side order.</param>
    /// <param name="Nodes">Shape and geometry-provider definitions.</param>
    /// <returns>Connection-boundary fractions per line, or null when the side's grid cannot hold the group.</returns>
    private static double[]? PlanGridFractions(List<PortEnd> Group, Dictionary<string, NodeContract> Nodes)
    {
        BoxRef Box = Group[0].Box;
        string Side = Group[0].Side;
        List<SideGridPoint> Points = SideGridPoints(Box, Side, Nodes, out double Length);
        if (Points.Count < Group.Count)
        {
            return null;
        }

        int[] Fixed = Enumerable.Repeat(-1, Group.Count).ToArray();
        HashSet<int> Taken = new HashSet<int>();
        for (int Index = 0; Index < Group.Count; Index++)
        {
            if (!Group[Index].KeepsCentre)
            {
                continue;
            }

            int Centre = Points.FindIndex(Point => Math.Abs(Point.Offset - (Length / 2.0)) <= DiagramGridMetrics.PORT_GRID_TOLERANCE_PX);
            if (Centre >= 0 && Taken.Add(Centre))
            {
                Fixed[Index] = Centre;
            }
        }

        for (int Index = 0; Index < Group.Count; Index++)
        {
            if (Fixed[Index] < 0
                && TryAlignedGridPoint(Group[Index], Points, Nodes, out int Aligned)
                && Taken.Add(Aligned))
            {
                Fixed[Index] = Aligned;
            }
        }

        int[]? Plan = ConnectionPointGrid.Plan(Points.Select(Point => Point.Offset).ToArray(), Length, Fixed);
        return Plan?.Select(Index => Points[Index].Fraction).ToArray();
    }

    /// <summary>Lists the usable grid points of one side of a placed node or frame.</summary>
    /// <param name="Box">Measured node or frame.</param>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="Nodes">Shape and geometry-provider definitions.</param>
    /// <param name="Length">Receives the length the grid is measured on.</param>
    /// <returns>Usable points in side order.</returns>
    private static List<SideGridPoint> SideGridPoints(BoxRef Box, string Side, Dictionary<string, NodeContract> Nodes,
        out double Length)
    {
        INodeConnectionBoundary? Boundary = Nodes.TryGetValue(Box.Id, out NodeContract? Node) && Node.GeometryProvider is { } Provider
            ? Provider.Create(Box.Width, Box.Height).ConnectionBoundary
            : null;
        double SideLength = Side is "left" or "right" ? Box.Height : Box.Width;
        return ConnectionPointGrid.UsablePoints(Boundary, Side, SideLength, out Length);
    }

    /// <summary>Finds the layout coordinate across the side (X for top and bottom, Y for left and right) of a grid point.</summary>
    /// <param name="Box">Measured node or frame.</param>
    /// <param name="Side">Top, bottom, left or right.</param>
    /// <param name="Point">Grid point on that side.</param>
    /// <param name="Nodes">Shape and geometry-provider definitions.</param>
    /// <returns>The absolute cross-side coordinate.</returns>
    private static double GridPointCross(BoxRef Box, string Side, SideGridPoint Point, Dictionary<string, NodeContract> Nodes)
    {
        bool Vertical = Side is "left" or "right";
        if (Nodes.TryGetValue(Box.Id, out NodeContract? Node) && Node.GeometryProvider is { } Provider
            && Provider.Create(Box.Width, Box.Height).ConnectionBoundary.TryPort(Side, Point.Fraction, out NodeGeometryPort Port))
        {
            return Vertical ? Box.CenterY - Port.Y : Box.CenterX + Port.X;
        }

        return Vertical ? Box.Top - Point.Offset : Box.Left + Point.Offset;
    }

    /// <summary>
    /// Finds the grid point that lets a line run straight to a peer whose facing side shares a grid coordinate
    /// with this side. Both ends make the same choice: the shared coordinate nearest both side centres.
    /// </summary>
    /// <param name="End">Line end being placed.</param>
    /// <param name="Points">Usable grid points on this end's side.</param>
    /// <param name="Nodes">Shape and geometry-provider definitions.</param>
    /// <param name="Index">Receives the index into <paramref name="Points"/>.</param>
    /// <returns>Whether a straight connection is available on the grid.</returns>
    private static bool TryAlignedGridPoint(PortEnd End, List<SideGridPoint> Points, Dictionary<string, NodeContract> Nodes,
        out int Index)
    {
        Index = -1;
        BoxRef Box = End.Box;
        BoxRef Other = End.Other;
        string Side = End.Side;
        string? Remote = End.IsSource ? End.Route.EntrySide : End.Route.ExitSide;
        string Facing = Side switch { "top" => "bottom", "bottom" => "top", "left" => "right", _ => "left" };
        if (Other.Id == Box.Id || !string.Equals(Remote, Facing, StringComparison.Ordinal))
        {
            return false;
        }

        bool Beyond = Side switch
        {
            "top" => Other.Bottom >= Box.Top - GRID_ALIGNMENT_TOLERANCE_PX,
            "bottom" => Other.Top <= Box.Bottom + GRID_ALIGNMENT_TOLERANCE_PX,
            "left" => Other.Right <= Box.Left + GRID_ALIGNMENT_TOLERANCE_PX,
            _ => Other.Left >= Box.Right - GRID_ALIGNMENT_TOLERANCE_PX
        };
        if (!Beyond || !UsesSideGrid(Box, Nodes) || !UsesSideGrid(Other, Nodes))
        {
            return false;
        }

        bool Vertical = Side is "left" or "right";
        double OwnCentre = Vertical ? Box.CenterY : Box.CenterX;
        double OtherCentre = Vertical ? Other.CenterY : Other.CenterX;
        List<SideGridPoint> OtherPoints = SideGridPoints(Other, Facing, Nodes, out _);
        double[] OtherCrosses = OtherPoints.Select(Point => GridPointCross(Other, Facing, Point, Nodes)).ToArray();
        double BestSum = double.PositiveInfinity;
        double BestMax = double.PositiveInfinity;
        double BestCross = double.PositiveInfinity;
        for (int Candidate = 0; Candidate < Points.Count; Candidate++)
        {
            double Cross = GridPointCross(Box, Side, Points[Candidate], Nodes);
            if (!OtherCrosses.Any(OtherCross => Math.Abs(OtherCross - Cross) <= GRID_ALIGNMENT_TOLERANCE_PX))
            {
                continue;
            }

            double Own = Math.Abs(Cross - OwnCentre);
            double Peer = Math.Abs(Cross - OtherCentre);
            double Sum = Math.Round(Own + Peer, 3);
            double Max = Math.Round(Math.Max(Own, Peer), 3);
            double Key = Math.Round(Cross, 3);
            if (Sum < BestSum || Sum == BestSum && (Max < BestMax || Max == BestMax && Key < BestCross))
            {
                BestSum = Sum;
                BestMax = Max;
                BestCross = Key;
                Index = Candidate;
            }
        }

        return Index >= 0;
    }

    /// <summary>Whether a node's ports come from its side grid rather than diamond or circle allocation.</summary>
    /// <param name="Box">Measured node or frame.</param>
    /// <param name="Nodes">Shape definitions.</param>
    /// <returns>True for boxes, frames and other side-allocated shapes.</returns>
    private static bool UsesSideGrid(BoxRef Box, Dictionary<string, NodeContract> Nodes) =>
        Box.IsFrame || !Nodes.TryGetValue(Box.Id, out NodeContract? Node)
            || !FlowchartDagrePresentation.IsDiamondShape(Node.Shape) && !IsCircleShape(Node.Shape);
}
