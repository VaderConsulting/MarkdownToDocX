using System.Collections.Generic;

namespace MDConverter.Model;

public sealed class DocModel
{
    private readonly List<BlockNode> _Blocks = [];

    public IReadOnlyList<BlockNode> Blocks => _Blocks;

    public void AddBlock(BlockNode Block)
    {
        _Blocks.Add(Block);
    }
}
