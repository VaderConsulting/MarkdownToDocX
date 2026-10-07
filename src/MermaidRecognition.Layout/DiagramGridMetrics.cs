using System;

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
    /// Clear space between facing outlines along the flow axis (rulebook 1, terms).
    /// </summary>
    public const int BASE_RANK_GAP_PX = 2 * GRID_UNIT_PX;

    /// <summary>
    /// Clear space between facing outlines across the flow axis (rulebook 1, terms).
    /// </summary>
    public const int BASE_COLUMN_GAP_PX = GRID_UNIT_PX;

    /// <summary>
    /// Minimum straight run after a port and before an arrowhead (rulebook decisions, 4.4).
    /// </summary>
    public const int MIN_STRAIGHT_RUN_PX = 50;

    /// <summary>
    /// Centre-to-centre spacing of tracks inside a rank gap (rulebook decisions).
    /// </summary>
    public const int TRACK_SPACING_PX = 6;

    /// <summary>
    /// Minimum clear span between node edges along the flow axis in LR diagrams.
    /// </summary>
    public const int MIN_LR_RANK_GAP_PX = BASE_RANK_GAP_PX;

    /// <summary>
    /// Column pitch for a standard node: the node plus one gap.
    /// </summary>
    public const int PITCH_X_PX = NODE_WIDTH_PX + NODE_GAP_PX;

    /// <summary>
    /// Row pitch for a standard node: the node plus one column gap.
    /// </summary>
    public const int PITCH_Y_PX = NODE_HEIGHT_PX + BASE_COLUMN_GAP_PX;

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
    /// <returns>Box height plus one column gap.</returns>
    public static int PitchY(int BoxHeightPx)
    {
        return BoxHeightPx + BASE_COLUMN_GAP_PX;
    }

    /// <summary>
    /// Centre-to-centre pitch along the flow axis for TB/BT ranks.
    /// </summary>
    /// <param name="BoxExtentPx">Node extent along the flow axis.</param>
    /// <returns>Extent plus the base rank gap.</returns>
    public static int PitchMainRank(int BoxExtentPx)
    {
        return BoxExtentPx + BASE_RANK_GAP_PX;
    }

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
