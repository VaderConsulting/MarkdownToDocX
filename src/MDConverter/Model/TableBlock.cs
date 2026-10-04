using System.Collections.Generic;

namespace MDConverter.Model;

public sealed class TableBlock : BlockNode
{
    private readonly List<TableRow> _Rows = [];

    public bool HasHeaderRow { get; set; } = true;

    public IReadOnlyList<TableAlignment> ColumnAlignments { get; set; } = new List<TableAlignment>();

    public IReadOnlyList<TableRow> Rows => _Rows;

    public void AddRow(TableRow Row)
    {
        _Rows.Add(Row);
    }
}

public enum TableAlignment
{
    Left,
    Center,
    Right
}

public sealed class TableRow
{
    private readonly List<TableCell> _Cells = [];

    public IReadOnlyList<TableCell> Cells => _Cells;

    public void AddCell(TableCell Cell)
    {
        _Cells.Add(Cell);
    }
}

public sealed class TableCell
{
    private readonly List<BlockNode> _Blocks = [];

    public IReadOnlyList<BlockNode> Blocks => _Blocks;

    public void AddBlock(BlockNode Block)
    {
        _Blocks.Add(Block);
    }
}
