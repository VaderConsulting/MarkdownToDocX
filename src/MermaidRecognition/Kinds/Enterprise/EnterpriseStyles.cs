using System.Text.RegularExpressions;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>Collects Mermaid style directives and resolves them after declarations.</summary>
public sealed class EnterpriseStyles
{
    public Dictionary<string, Dictionary<string, string>> Classes { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Dictionary<string, string>> Elements { get; } = new(StringComparer.Ordinal);
    public Dictionary<int, Dictionary<string, string>> Links { get; } = [];
    public Dictionary<string, string> DefaultLink { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Kind, string Targets, string Value, int Line)> Pending = [];
    private readonly Dictionary<string, List<string>> InlineClasses = new(StringComparer.Ordinal);

    internal bool Read(string Text, int Line)
    {
        Match Match = Regex.Match(Text, @"^(classDef|class|style|linkStyle)\s+(\S+)\s+(.+)$", RegexOptions.IgnoreCase);
        if (!Match.Success)
        {
            return false;
        }

        Pending.Add((Match.Groups[1].Value.ToLowerInvariant(), Match.Groups[2].Value, Match.Groups[3].Value, Line));
        return true;
    }

    internal void AddInlineClass(string Id, string Class)
    {
        if (!InlineClasses.TryGetValue(Id, out List<string>? Names))
        {
            InlineClasses[Id] = Names = [];
        }

        Names.Add(Class);
    }

    internal void Resolve(EnterpriseDocument Document, List<EnterpriseDiagnostic> Diagnostics)
    {
        foreach ((string Kind, string Targets, string Value, int Line) Directive in Pending.Where(Item => Item.Kind == "classdef"))
        {
            foreach (string Class in Directive.Targets.Split(','))
            {
                if (!Classes.TryGetValue(Class, out Dictionary<string, string>? Style))
                {
                    Classes[Class] = Style = new(StringComparer.OrdinalIgnoreCase);
                }

                Merge(Style, ParseProperties(Directive.Value));
            }
        }

        foreach (string Id in Document.Elements.Keys)
        {
            Elements[Id] = new(StringComparer.OrdinalIgnoreCase);
            if (Classes.TryGetValue("default", out Dictionary<string, string>? Default))
            {
                Merge(Elements[Id], Default);
            }

            if (InlineClasses.TryGetValue(Id, out List<string>? Names))
            {
                foreach (string Name in Names)
                {
                    if (Classes.TryGetValue(Name, out Dictionary<string, string>? Style))
                    {
                        Merge(Elements[Id], Style);
                    }
                }
            }
        }
        foreach ((string Kind, string Targets, string Value, int Line) Directive in Pending.Where(Item => Item.Kind != "classdef"))
        {
            if (Directive.Kind == "linkstyle")
            {
                Dictionary<string, string> Properties = ParseProperties(Regex.Replace(Directive.Value, @"^interpolate\s+\S+\s*", "", RegexOptions.IgnoreCase));
                if (Directive.Targets.Equals("default", StringComparison.OrdinalIgnoreCase)) { Merge(DefaultLink, Properties); continue; }
                string[] Targets = Directive.Targets.Split(',');
                if (Targets.Any(Target => !int.TryParse(Target, out int Index) || Index < 0 || Index >= Document.Relationships.Count))
                {
                    Diagnostics.Add(new EnterpriseDiagnostic(MermaidDiagnosticSeverity.Error, Directive.Line,
                        $"line {Directive.Line}: linkStyle index is outside the declared relationship range"));
                    continue;
                }
                foreach (string Target in Targets)
                {
                    int Index = int.Parse(Target, System.Globalization.CultureInfo.InvariantCulture);
                    if (!Links.TryGetValue(Index, out Dictionary<string, string>? Style))
                    {
                        Links[Index] = Style = new(StringComparer.OrdinalIgnoreCase);
                    }

                    Merge(Style, Properties);
                }
                continue;
            }
            foreach (string Target in Directive.Targets.Split(','))
            {
                if (!Elements.TryGetValue(Target, out Dictionary<string, string>? Style))
                {
                    Diagnostics.Add(new EnterpriseDiagnostic(MermaidDiagnosticSeverity.Warning, Directive.Line,
                        $"line {Directive.Line}: {Directive.Kind} names undeclared element \"{Target}\"; ignored"));
                    continue;
                }
                if (Directive.Kind == "style")
                {
                    Merge(Style, ParseProperties(Directive.Value));
                }
                else
                {
                    foreach (string Name in Directive.Value.Split(',', StringSplitOptions.TrimEntries))
                    {
                        if (Classes.TryGetValue(Name, out Dictionary<string, string>? ClassStyle))
                        {
                            Merge(Style, ClassStyle);
                        }
                    }
                }
            }
        }
    }

    private static Dictionary<string, string> ParseProperties(string Text)
    {
        Dictionary<string, string> Result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string Property in Regex.Split(Text, @"(?<!\\),"))
        {
            int Colon = Property.IndexOf(':');
            if (Colon <= 0)
            {
                continue;
            }

            string Key = Property[..Colon].Trim();
            if (Regex.IsMatch(Key, @"^[A-Za-z][A-Za-z-]*$"))
            {
                Result[Key] = Property[(Colon + 1)..].Trim().Replace(@"\,", ",", StringComparison.Ordinal);
            }
        }
        return Result;
    }

    private static void Merge(Dictionary<string, string> Target, Dictionary<string, string> Source)
    {
        foreach (KeyValuePair<string, string> Pair in Source)
        {
            Target[Pair.Key] = Pair.Value;
        }
    }
}
