using System.Text.RegularExpressions;
using MermaidRecognition.Contract;

namespace MermaidRecognition.Flowchart;

/// <summary>
/// Parses edge metadata mutation lines such as <c>e1@{ curve: basis }</c> (vendored flow-lines.spec.js).
/// </summary>
public static class FlowchartEdgeMetadataParser
{
    private static readonly Regex EdgeMetadataLineRegex = new(
        @"^\s*([A-Za-z][\w-]*)@\{(.+)\}\s*$",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns true when the line updates an existing edge by id.
    /// </summary>
    public static bool TryParse(string Line, FlowchartDb Database)
    {
        Match Match = EdgeMetadataLineRegex.Match(Line);
        if (!Match.Success)
        {
            return false;
        }

        string EdgeId = Match.Groups[1].Value;
        string MetadataBody = Match.Groups[2].Value.Trim();
        FlowchartEdgeRecord? Edge = Database.FindEdgeById(EdgeId);
        if (Edge is null)
        {
            return false;
        }

        foreach (string Entry in FlowchartNodeDeclarationParser.SplitMetadataEntries(MetadataBody))
        {
            ApplyMetadataEntry(Edge, Entry);
        }

        return true;
    }

    static void ApplyMetadataEntry(FlowchartEdgeRecord Edge, string Entry)
    {
        int ColonIndex = Entry.IndexOf(':');
        if (ColonIndex < 0)
        {
            return;
        }

        string Key = Entry[..ColonIndex].Trim();
        string Value = Entry[(ColonIndex + 1)..].Trim().Trim('"', '\'');
        if (Key.Length == 0)
        {
            return;
        }

        if (string.Equals(Key, "animate", StringComparison.OrdinalIgnoreCase))
        {
            bool? Animate = ParseBoolean(Value);
            AnimationMetadata Existing = GetExistingAnimationMetadata(Edge);
            Edge.MetadataMutable["animate"] = new AnimationMetadata(Animate, Existing?.Animation);
            return;
        }

        if (string.Equals(Key, "animation", StringComparison.OrdinalIgnoreCase))
        {
            AnimationMetadata Existing = GetExistingAnimationMetadata(Edge);
            Edge.MetadataMutable["animate"] = new AnimationMetadata(Existing?.Animate, Value);
            return;
        }

        if (string.Equals(Key, "curve", StringComparison.OrdinalIgnoreCase))
        {
            Edge.MetadataMutable["curve"] = new CurveMetadata(Value);
            return;
        }

        Edge.MetadataMutable[Key] = new RawMetadata(Key, Value);
    }

    static AnimationMetadata? GetExistingAnimationMetadata(FlowchartEdgeRecord Edge)
    {
        if (Edge.MetadataMutable.TryGetValue("animate", out MermaidMetadataValue? Existing)
            && Existing is AnimationMetadata Animation)
        {
            return Animation;
        }

        return null;
    }

    static bool? ParseBoolean(string Value)
    {
        if (string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return null;
    }
}
