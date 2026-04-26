using System.Collections.Generic;

namespace MDConverter.Model;

public sealed class DocModel
{
    private readonly List<BlockNode> _Blocks = new List<BlockNode>();

    public IReadOnlyList<BlockNode> Blocks
    {
        get { return _Blocks; }
    }

    public void AddBlock(BlockNode Block)
    {
        _Blocks.Add(Block);
    }
}
