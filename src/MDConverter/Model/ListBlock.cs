using System.Collections.Generic;

namespace MDConverter.Model;

/// <summary>Ordered or unordered list; contains list items.</summary>
public sealed class ListBlock : BlockNode
{
    private readonly List<ListItemBlock> _Items = new List<ListItemBlock>();

    /// <summary>When true, list is ordered (numbered); otherwise unordered (spec name: Ordered).</summary>
    public bool IsOrdered { get; }

    /// <summary>Spec-compliant alias for IsOrdered.</summary>
    public bool Ordered
    {
        get { return IsOrdered; }
    }

    /// <summary>Optional start number for ordered lists (e.g. 3 for "3. Item").</summary>
    public int? StartNumber { get; set; }

    public IReadOnlyList<ListItemBlock> Items => _Items;

    public ListBlock(bool IsOrdered) => this.IsOrdered = IsOrdered;

    public void AddItem(ListItemBlock Item)
    {
        _Items.Add(Item);
    }
}
