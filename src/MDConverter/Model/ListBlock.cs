using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Ordered or unordered list; contains list items.</summary>
public sealed class ListBlock(bool IsOrdered) : BlockNode
{
    private readonly List<ListItemBlock> _Items = [];

    /// <summary>When true, list is ordered (numbered); otherwise unordered (spec name: Ordered).</summary>
    public bool IsOrdered { get; } = IsOrdered;

    /// <summary>Spec-compliant alias for IsOrdered.</summary>
    public bool Ordered => IsOrdered;

    /// <summary>Optional start number for ordered lists (e.g. 3 for "3. Item").</summary>
    public int? StartNumber { get; set; }

    public IReadOnlyList<ListItemBlock> Items => _Items;

    public void AddItem(ListItemBlock Item)
    {
        _Items.Add(Item);
    }
}
