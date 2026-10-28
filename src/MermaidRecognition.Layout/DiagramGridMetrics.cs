using System;
using System.Collections.Generic;
using System.Linq;

namespace MermaidRecognition.Layout;

/// <summary>
/// Shared spacing for every diagram the layout engine places.
/// Values are whole pixels. They are not adjusted per diagram.
/// </summary>
public static class DiagramGridMetrics
{
    /// <summary>
    /// Base unit. Every other distance is a whole number of these.
    /// </summary>
    public const int GRID_UNIT_PX = 30;

    /// <summary>
    /// Standard node width.
    /// </summary>
    public const int NODE_WIDTH_PX = 5 * GRID_UNIT_PX;

    /// <summary>
    /// Standard node height.
    /// </summary>
    public const int NODE_HEIGHT_PX = 2 * GRID_UNIT_PX;

    /// <summary>
    /// Gap between neighbouring nodes, and the even inset inside a group frame.
    /// </summary>
    public const int NODE_GAP_PX = GRID_UNIT_PX;

    /// <summary>
    /// Minimum clear span between node edges along the flow axis in LR diagrams.
    /// </summary>
    public const int MIN_LR_RANK_GAP_PX = 49;

    /// <summary>
    /// Column pitch for a standard node: the node plus one gap.
    /// </summary>
    public const int PITCH_X_PX = NODE_WIDTH_PX + NODE_GAP_PX;

    /// <summary>
    /// Row pitch for a standard node: the node plus one gap.
    /// </summary>
    public const int PITCH_Y_PX = NODE_HEIGHT_PX + NODE_GAP_PX;

    /// <summary>
    /// How many line tracks fit in one gap.
    /// </summary>
    public const int TRACKS_PER_GAP = 5;

    /// <summary>
    /// Centres within this distance, and not in the same rank, share a column or row.
    /// Wider than a rank band, and narrower than a grid unit, so only lines Mermaid
    /// already drew as one line are snapped together.
    /// </summary>
    public const int ALIGNMENT_TOLERANCE_PX = 8;

    /// <summary>
    /// Centres closer than this on the rank axis are the same rank.
    /// </summary>
    public const int RANK_BAND_TOLERANCE_PX = 2;

    /// <summary>
    /// Inset between label glyphs and the inside of the standard box.
    /// </summary>
    public const int TEXT_INSET_PX = 8;

    /// <summary>
    /// Line height used when deciding whether a wrapped label fits the box.
    /// </summary>
    public const double TEXT_LINE_HEIGHT_EM = 1.25;

    /// <summary>
    /// Rounds a positive distance up to a whole number of grid units.
    /// </summary>
    /// <param name="ValuePx">Distance in pixels.</param>
    /// <returns>The next whole grid multiple, or zero when the value is not positive.</returns>
    public static int CeilToGrid(double ValuePx)
    {
        if (ValuePx <= 0.0)
        {
            return 0;
        }

        return (int)(Math.Ceiling((ValuePx / GRID_UNIT_PX) - 1e-9) * GRID_UNIT_PX);
    }

    /// <summary>
    /// Rounds a positive distance to the nearest whole number of grid units.
    /// </summary>
    /// <param name="ValuePx">Distance in pixels.</param>
    /// <returns>The nearest grid multiple, or zero when the value is not positive.</returns>
    public static int RoundToGrid(double ValuePx)
    {
        if (ValuePx <= 0.0)
        {
            return 0;
        }

        return (int)(Math.Round(ValuePx / GRID_UNIT_PX) * GRID_UNIT_PX);
    }

    /// <summary>
    /// Grows the standard box in whole grid units until the label fits.
    /// </summary>
    /// <param name="NeededWidthPx">Label width including inset.</param>
    /// <param name="NeededHeightPx">Label height including inset.</param>
    /// <returns>The box width and height.</returns>
    public static (int WidthPx, int HeightPx) GrowBox(double NeededWidthPx, double NeededHeightPx)
    {
        int WidthPx = NODE_WIDTH_PX;
        int HeightPx = NODE_HEIGHT_PX;
        if (NeededWidthPx > WidthPx)
        {
            WidthPx = Math.Max(WidthPx, CeilToGrid(NeededWidthPx));
        }

        if (NeededHeightPx > HeightPx)
        {
            HeightPx = Math.Max(HeightPx, CeilToGrid(NeededHeightPx));
        }

        return (WidthPx, HeightPx);
    }

    /// <summary>
    /// Column pitch once the shared box has grown.
    /// </summary>
    /// <param name="BoxWidthPx">Shared box width.</param>
    /// <returns>Box width plus one gap.</returns>
    public static int PitchX(int BoxWidthPx)
    {
        return BoxWidthPx + NODE_GAP_PX;
    }

    /// <summary>
    /// Row pitch once the shared box has grown.
    /// </summary>
    /// <param name="BoxHeightPx">Shared box height.</param>
    /// <returns>Box height plus one gap.</returns>
    public static int PitchY(int BoxHeightPx)
    {
        return BoxHeightPx + NODE_GAP_PX;
    }

    /// <summary>
    /// Maximum parallel tracks that fit in a gap while keeping line_spacing clearance from each outline.
    /// </summary>
    /// <param name="GapPx">Gap width in pixels.</param>
    /// <returns>Track capacity from term Track.</returns>
    public static int MaxTracksInGap(int GapPx)
    {
        if (GapPx < 1)
        {
            return 0;
        }

        return Math.Max(0, (int)Math.Floor(GapPx / FlowchartLayoutRules.LineSpacing) - 1);
    }

    /// <summary>
    /// Minimum gap width that holds the requested number of tracks with line_spacing insets.
    /// </summary>
    /// <param name="TrackCount">Forecast tracks that must fit.</param>
    /// <returns>Required interior span in pixels.</returns>
    public static int GapWidthForTracks(int TrackCount)
    {
        if (TrackCount < 1)
        {
            return 0;
        }

        return (TrackCount + 1) * (int)Math.Round(FlowchartLayoutRules.LineSpacing);
    }

    /// <summary>
    /// Maximum ports that fit on one box side: the number of connection points on its centre-out grid.
    /// </summary>
    /// <param name="SideLengthPx">Visible side length in pixels.</param>
    /// <returns>Geometric maximum from rule 3.2 and term Port.</returns>
    public static int MaxPortsOnSide(int SideLengthPx) => SideGridOffsets(SideLengthPx).Length;

    /// <summary>
    /// Minimum side length whose centre-out grid holds the requested port count in the default pattern.
    /// An odd count uses the centre and needs (n + 1) line_spacing; an even count leaves the centre empty and
    /// needs (n + 2) line_spacing.
    /// </summary>
    /// <param name="PortCount">Jointly allocated ports on one side.</param>
    /// <returns>Required side span in pixels.</returns>
    public static int MinSideLengthForPorts(int PortCount)
    {
        if (PortCount < 1)
        {
            return 0;
        }

        int Spacings = PortCount % 2 == 1 ? PortCount + 1 : PortCount + 2;
        return Spacings * (int)Math.Round(FlowchartLayoutRules.LineSpacing);
    }

    /// <summary>
    /// Connection points on one side, counted out from its centre: the midpoint, then every line_spacing either
    /// side of it, keeping only those at least line_spacing from both corners (term Port, rule 3.2).
    /// </summary>
    /// <param name="LengthPx">Side or connection-segment length.</param>
    /// <returns>Distances from the start of the side in ascending order; empty when the side is too short.</returns>
    public static double[] SideGridOffsets(double LengthPx)
    {
        double Spacing = FlowchartLayoutRules.LineSpacing;
        double Middle = LengthPx / 2.0;
        if (Spacing <= 0 || !double.IsFinite(LengthPx) || Middle < Spacing - PORT_GRID_TOLERANCE_PX)
        {
            return [];
        }

        int Steps = (int)Math.Floor(((Middle - Spacing) + PORT_GRID_TOLERANCE_PX) / Spacing);
        double[] Offsets = new double[(2 * Steps) + 1];
        for (int Step = -Steps; Step <= Steps; Step++)
        {
            Offsets[Step + Steps] = Middle + (Step * Spacing);
        }

        return Offsets;
    }

    /// <summary>
    /// Default connection points for lines sharing one side (rule 3.2): an odd count takes the centre and then
    /// one line_spacing step either side in turn; an even count takes the steps either side and leaves the
    /// centre empty.
    /// </summary>
    /// <param name="PortCount">Lines sharing the side.</param>
    /// <param name="LengthPx">Side or connection-segment length.</param>
    /// <returns>Offsets from the start of the side in ascending order; fewer than requested when the side is too short.</returns>
    public static double[] CentreOutPortOffsets(int PortCount, double LengthPx)
    {
        double[] Grid = SideGridOffsets(LengthPx);
        int[] Indexes = CentreOutGridIndexes(Grid, LengthPx, PortCount);
        double[] Offsets = new double[Indexes.Length];
        for (int Index = 0; Index < Indexes.Length; Index++)
        {
            Offsets[Index] = Grid[Indexes[Index]];
        }

        return Offsets;
    }

    /// <summary>
    /// Chooses the default centre-out pattern from any set of usable grid points on a side.
    /// </summary>
    /// <param name="Grid">Usable grid offsets in ascending order.</param>
    /// <param name="LengthPx">Full side length; its midpoint is the side centre.</param>
    /// <param name="PortCount">Lines sharing the side.</param>
    /// <returns>Indexes into <paramref name="Grid"/> in ascending order.</returns>
    public static int[] CentreOutGridIndexes(IReadOnlyList<double> Grid, double LengthPx, int PortCount)
    {
        if (PortCount < 1 || Grid.Count == 0)
        {
            return [];
        }

        double Middle = LengthPx / 2.0;
        List<int> Ranked = new List<int>(Grid.Count);
        for (int Index = 0; Index < Grid.Count; Index++)
        {
            Ranked.Add(Index);
        }

        Ranked.Sort((Left, Right) =>
        {
            int Order = Math.Abs(Grid[Left] - Middle).CompareTo(Math.Abs(Grid[Right] - Middle));
            return Order != 0 ? Order : Left.CompareTo(Right);
        });
        int Skip = PortCount % 2 == 0
            && Grid.Count > PortCount
            && Math.Abs(Grid[Ranked[0]] - Middle) <= PORT_GRID_TOLERANCE_PX ? 1 : 0;
        int[] Chosen = [.. Ranked.Skip(Skip).Take(PortCount)];
        Array.Sort(Chosen);
        return Chosen;
    }

    /// <summary>Tolerance used when testing whether a distance reaches a corner clearance, in pixels.</summary>
    public const double PORT_GRID_TOLERANCE_PX = 0.01;

    /// <summary>
    /// Equal slot or track centres along a side or gap, symmetrical about the centre.
    /// </summary>
    /// <param name="LengthPx">Side or gap length.</param>
    /// <param name="SlotCount">How many equal positions span that length.</param>
    /// <returns>Centres measured from the start of the side or gap.</returns>
    public static int[] EqualCentres(int LengthPx, int SlotCount)
    {
        if (SlotCount < 1 || LengthPx < 1)
        {
            return [];
        }

        int[] Centres = new int[SlotCount];
        int Previous = -1;
        for (int Index = 0; Index < SlotCount; Index++)
        {
            double Raw = ((Index + 0.5) * LengthPx) / SlotCount;
            int Centre = (int)Math.Round(Raw, MidpointRounding.AwayFromZero);
            if (Centre <= Previous)
            {
                Centre = Previous + 1;
            }

            if (Centre >= LengthPx)
            {
                Centre = LengthPx - 1;
            }

            Centres[Index] = Centre;
            Previous = Centre;
        }

        int MirrorError = Centres[0] - (LengthPx - Centres[SlotCount - 1]);
        if (MirrorError != 0 && SlotCount > 1)
        {
            Centres[SlotCount - 1] = LengthPx - Centres[0];
        }

        return Centres;
    }

    /// <summary>
    /// Which of the equal centres are used for a given line count.
    /// Five centres use the standard pattern. A longer side uses the same idea from the centre outward.
    /// </summary>
    /// <param name="LineCount">How many lines share the side or gap.</param>
    /// <param name="SlotCount">How many centres exist.</param>
    /// <returns>Indexes into the centre array, in left-to-right or top-to-bottom order.</returns>
    public static int[] SelectSlotIndexes(int LineCount, int SlotCount)
    {
        if (LineCount < 1 || SlotCount < 1)
        {
            return [];
        }

        if (LineCount >= SlotCount)
        {
            int[] All = new int[SlotCount];
            for (int Index = 0; Index < SlotCount; Index++)
            {
                All[Index] = Index;
            }

            return All;
        }

        if (SlotCount == TRACKS_PER_GAP)
        {
            if (LineCount == 1)
            {
                return [2];
            }

            if (LineCount == 2)
            {
                return [1, 3];
            }

            if (LineCount == 3)
            {
                return [1, 2, 3];
            }

            if (LineCount == 4)
            {
                return [0, 1, 3, 4];
            }
        }

        int[] Chosen = new int[LineCount];
        int Middle = SlotCount / 2;
        if (LineCount == 1)
        {
            Chosen[0] = Middle;
            return Chosen;
        }

        int Span = SlotCount - 1;
        for (int Index = 0; Index < LineCount; Index++)
        {
            Chosen[Index] = (int)Math.Round((Index * (double)Span) / (LineCount - 1), MidpointRounding.AwayFromZero);
        }

        return Chosen;
    }

    /// <summary>
    /// Track centres across a gap, widening by whole grid units when more than five lines share it.
    /// </summary>
    /// <param name="LineCount">How many lines share the gap.</param>
    /// <param name="GapPx">The gap length after any extra grid steps.</param>
    /// <returns>Offsets from the start of the gap, one per line, in cross-axis order.</returns>
    public static int[] TrackOffsets(int LineCount, int GapPx)
    {
        int Units = Math.Max(1, (LineCount + TRACKS_PER_GAP - 1) / TRACKS_PER_GAP);
        int LengthPx = Math.Max(GapPx, Units * GRID_UNIT_PX);
        int SlotCount = Units * TRACKS_PER_GAP;
        int[] Centres = EqualCentres(LengthPx, SlotCount);
        int[] Indexes = SelectSlotIndexes(LineCount, SlotCount);
        int[] Offsets = new int[Indexes.Length];
        for (int Index = 0; Index < Indexes.Length; Index++)
        {
            Offsets[Index] = Centres[Indexes[Index]];
        }

        return Offsets;
    }
}
