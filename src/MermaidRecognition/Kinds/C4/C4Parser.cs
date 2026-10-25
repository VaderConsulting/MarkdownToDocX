using System.Collections.Generic;
using System.Text.RegularExpressions;
using MermaidRecognition.Core;

namespace MermaidRecognition.Kinds.C4;

/// <summary>
/// Parses C4 diagrams into line-number items for the LiveEditor UI.
/// </summary>
public sealed class C4Parser : HeaderKindParser
{
    private static readonly string[] Headers = new string[]
    {
        "C4Context",
        "C4Container",
        "C4Component",
        "C4Dynamic",
        "C4Deployment"
    };

    private static readonly Regex ElementRegex = new Regex(
        @"^(?<type>Person|Person_Ext|System|System_Ext|SystemDb|SystemDb_Ext|SystemQueue|SystemQueue_Ext|Container|Container_Ext|ContainerDb|ContainerDb_Ext|ContainerQueue|ContainerQueue_Ext|Component|Component_Ext|ComponentDb|ComponentDb_Ext|ComponentQueue|ComponentQueue_Ext|Node|Node_L|Deployment_Node|Enterprise_Boundary|System_Boundary|Container_Boundary|Boundary)\s*\(\s*(?<id>[A-Za-z_][\w-]*)\s*(?:,\s*""(?<label>[^""]*)"")?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RelRegex = new Regex(
        @"^(?<type>Rel|BiRel|Rel_U|Rel_D|Rel_L|Rel_R|Rel_Back)\s*\(\s*(?<src>[A-Za-z_][\w-]*)\s*,\s*(?<tgt>[A-Za-z_][\w-]*)\s*(?:,\s*""(?<label>[^""]*)"")?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Shared instance used by <see cref="Parser.ForSource"/>.</summary>
    public static C4Parser Default { get; } = new C4Parser();

    static C4Parser()
    {
        Parser.Register(Default);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<string> HeaderPrefixes
    {
        get
        {
            return Headers;
        }
    }

    /// <inheritdoc />
    protected override bool TryMapLine(string Trimmed, int LineNumber, out DiagramItem? Item)
    {
        Item = null;
        Match Element = ElementRegex.Match(Trimmed);
        if (Element.Success)
        {
            string Id = Element.Groups["id"].Value;
            string Label = Element.Groups["label"].Success ? Element.Groups["label"].Value : Id;
            string Type = Element.Groups["type"].Value;
            bool Boundary = Type.Contains("Boundary", System.StringComparison.OrdinalIgnoreCase);
            Item = Boundary
                ? Container(LineNumber, Id, Label, Type)
                : Node(LineNumber, Id, Label, Type);
            return true;
        }

        Match Rel = RelRegex.Match(Trimmed);
        if (Rel.Success)
        {
            string Label = Rel.Groups["label"].Success ? Rel.Groups["label"].Value : string.Empty;
            Item = Relationship(LineNumber, Rel.Groups["src"].Value, Rel.Groups["tgt"].Value, Label, Rel.Groups["type"].Value);
            return true;
        }

        return false;
    }
}
