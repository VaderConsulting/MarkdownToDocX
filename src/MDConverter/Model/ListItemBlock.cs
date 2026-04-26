using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>
/// A single item in a list; contains a sequence of block nodes.
/// </summary>
public sealed class ListItemBlock : BlockNode
{
    private readonly List<BlockNode> _Blocks = new List<BlockNode>();

    /// <summary>
    /// Blocks inside this list item.
    /// </summary>
    public IReadOnlyList<BlockNode> Blocks
    {
        get
        {
            return _Blocks;
        }
    }

    /// <summary>
    /// Adds a block to the list item.
    /// </summary>
    public void AddBlock(BlockNode Block)
    {
        _Blocks.Add(Block);
    }
}
