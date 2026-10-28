using System.Collections.Generic;

namespace MermaidRecognition.Core;

/// <summary>
/// Shared outcome of <see cref="Parser.Parse"/> for every diagram kind.
/// </summary>
public sealed class DiagramParseResult
{
    private readonly List<DiagramItem> _Items;

    private readonly List<string> _Diagnostics;

    /// <summary>
    /// Creates a parse result.
    /// </summary>
    /// <param name="Items">Items keyed by source line id.</param>
    /// <param name="Diagnostics">Human-readable diagnostics, one message each.</param>
    public DiagramParseResult(IReadOnlyList<DiagramItem>? Items, IReadOnlyList<string>? Diagnostics)
    {
        _Items = Items == null ? [] : new List<DiagramItem>(Items);
        _Diagnostics = Diagnostics == null ? [] : new List<string>(Diagnostics);
    }

    /// <summary>Gets the declared items. At most one per source line.</summary>
    public IReadOnlyList<DiagramItem> Items
    {
        get
        {
            return _Items;
        }
    }

    /// <summary>Gets parser diagnostics as plain messages.</summary>
    public IReadOnlyList<string> Diagnostics
    {
        get
        {
            return _Diagnostics;
        }
    }

    /// <summary>
    /// Finds the item on a 1-based line, or null when that line does not declare one.
    /// </summary>
    /// <param name="LineNumber">1-based line number.</param>
    /// <returns>The item, or null.</returns>
    public DiagramItem? ItemAtLine(int LineNumber)
    {
        if (LineNumber < 1)
        {
            return null;
        }

        foreach (DiagramItem Item in _Items)
        {
            if (Item.Id == LineNumber)
            {
                return Item;
            }
        }

        return null;
    }
}
