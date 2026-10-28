using System;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Layout;

internal static partial class FlowchartPlacementHints
{
    internal static (int Row, int Column) DiagonalSigns(PlacementHintKind Kind) => Kind switch
    {
        PlacementHintKind.NorthWest => (-1, -1),
        PlacementHintKind.NorthEast => (-1, 1),
        PlacementHintKind.SouthEast => (1, 1),
        PlacementHintKind.SouthWest => (1, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(Kind))
    };

    internal static FlowchartCellRegion DiagonalTarget(PlacementHintContract Hint, FlowchartCellRegion Owner, FlowchartCellRegion Peer)
    {
        var (Row, Column) = DiagonalSigns(Hint.Kind);
        return new(Row < 0 ? checked(Peer.StartRow - Owner.RowSpan - Hint.Offset) : checked(Peer.EndRow + Hint.Offset),
            Column < 0 ? checked(Peer.StartColumn - Owner.ColumnSpan - (Hint.ColumnOffset ?? Hint.Offset))
                : checked(Peer.EndColumn + (Hint.ColumnOffset ?? Hint.Offset)), Owner.RowSpan, Owner.ColumnSpan);
    }
}
