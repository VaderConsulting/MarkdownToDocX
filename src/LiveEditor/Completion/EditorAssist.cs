using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Completion;

/// <summary>
/// Chooses enterprise or Mermaid assistance for the caret.
/// </summary>
internal sealed class EditorAssist
{
    private readonly EnterpriseCompletionEngine _Enterprise = new EnterpriseCompletionEngine();

    private readonly MermaidDiagramAssist _Mermaid = new MermaidDiagramAssist();

    /// <summary>List Members for the caret.</summary>
    public CompletionContext BuildMembers(string Source, int CaretIndex)
    {
        if (EnterpriseParser.IsEnterpriseSource(Source))
        {
            return _Enterprise.Build(Source, CaretIndex);
        }

        return _Mermaid.BuildMembers(Source, CaretIndex);
    }

    /// <summary>Parameter Info for the caret, or null.</summary>
    public ParameterHelp? BuildParameter(string Source, int CaretIndex)
    {
        if (EnterpriseParser.IsEnterpriseSource(Source))
        {
            return _Enterprise.BuildParameterHelp(Source, CaretIndex);
        }

        return _Mermaid.BuildParameter(Source, CaretIndex);
    }

    /// <summary>Quick Info for the word at <paramref name="CaretIndex"/>, or null.</summary>
    public string? BuildQuickInfo(string Source, int CaretIndex)
    {
        if (EnterpriseParser.IsEnterpriseSource(Source))
        {
            return _Enterprise.BuildQuickInfo(Source, CaretIndex);
        }

        return _Mermaid.BuildQuickInfo(Source, CaretIndex);
    }

    /// <summary>
    /// True when the caret line already declares an element, group, note, or relationship.
    /// </summary>
    public bool IsDeclaredLine(string Source, int CaretIndex)
    {
        if (!EnterpriseParser.IsEnterpriseSource(Source))
        {
            return false;
        }

        return _Enterprise.IsDeclaredLine(Source, CaretIndex);
    }

    /// <summary>One-line signature for the status bar.</summary>
    public string BuildSignatureHelp(string Source, int CaretIndex)
    {
        if (EnterpriseParser.IsEnterpriseSource(Source))
        {
            return _Enterprise.BuildSignatureHelp(Source, CaretIndex);
        }

        ParameterHelp? Help = _Mermaid.BuildParameter(Source, CaretIndex);
        if (Help == null)
        {
            return string.Empty;
        }

        return Help.Signature;
    }
}
