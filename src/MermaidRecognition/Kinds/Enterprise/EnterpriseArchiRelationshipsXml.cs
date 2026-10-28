using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Reads Archi <c>relationships.xml</c> into the kebab-case letter matrix used by
/// <see cref="EnterpriseRulesCatalog"/>. Junction-as-target cells follow Archi
/// (every relationship letter in 3.2). And/or polarity stays in the parser and renderer.
/// </summary>
public static class EnterpriseArchiRelationshipsXml
{
    /// <summary>
    /// Builds a types.Count × types.Count letter matrix from an Archi relationships document.
    /// Concepts that are not in <paramref name="Types"/> (for example <c>Relationship</c>) are ignored.
    /// </summary>
    /// <param name="Xml">Archi relationships.xml text.</param>
    /// <param name="Types">Kebab-case type order matching <c>enterprise-rules.json</c>.</param>
    /// <returns>Allowed-letter rows.</returns>
    public static string[][] BuildMatrix(string Xml, IReadOnlyList<string> Types)
    {
        if (string.IsNullOrWhiteSpace(Xml))
        {
            throw new ArgumentException("Archi relationships XML is required.", nameof(Xml));
        }

        if (Types == null || Types.Count == 0)
        {
            throw new ArgumentException("Type order is required.", nameof(Types));
        }

        Dictionary<string, int> IndexByConcept = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int Index = 0; Index < Types.Count; Index++)
        {
            IndexByConcept[ToArchiConcept(Types[Index])] = Index;
        }

        string[][] Matrix = new string[Types.Count][];
        for (int Row = 0; Row < Types.Count; Row++)
        {
            Matrix[Row] = new string[Types.Count];
            for (int Column = 0; Column < Types.Count; Column++)
            {
                Matrix[Row][Column] = string.Empty;
            }
        }

        XDocument Document = XDocument.Parse(Xml);
        XElement? Root = Document.Root;
        if (Root == null)
        {
            throw new InvalidOperationException("Archi relationships XML has no root element.");
        }

        foreach (XElement Source in Root.Elements("source"))
        {
            string? SourceConcept = (string?)Source.Attribute("concept");
            if (string.IsNullOrEmpty(SourceConcept) || !IndexByConcept.TryGetValue(SourceConcept, out int SourceIndex))
            {
                continue;
            }

            foreach (XElement Target in Source.Elements("target"))
            {
                string? TargetConcept = (string?)Target.Attribute("concept");
                if (string.IsNullOrEmpty(TargetConcept) || !IndexByConcept.TryGetValue(TargetConcept, out int TargetIndex))
                {
                    continue;
                }

                Matrix[SourceIndex][TargetIndex] = NormaliseLetters((string?)Target.Attribute("relations") ?? string.Empty);
            }
        }

        return Matrix;
    }

    /// <summary>
    /// Converts a kebab-case enterprise type id to the Archi concept name.
    /// </summary>
    /// <param name="KebabName">Kebab-case type.</param>
    /// <returns>PascalCase concept name.</returns>
    public static string ToArchiConcept(string KebabName)
    {
        if (string.IsNullOrEmpty(KebabName))
        {
            return string.Empty;
        }

        StringBuilder Builder = new StringBuilder();
        bool Capitalise = true;
        foreach (char Character in KebabName)
        {
            if (Character == '-')
            {
                Capitalise = true;
                continue;
            }

            Builder.Append(Capitalise ? char.ToUpper(Character, CultureInfo.InvariantCulture) : Character);
            Capitalise = false;
        }

        return Builder.ToString();
    }

    /// <summary>
    /// Sorts relationship letters so catalog cells compare stably.
    /// </summary>
    /// <param name="Letters">Archi relations attribute.</param>
    /// <returns>Sorted unique letters.</returns>
    public static string NormaliseLetters(string Letters)
    {
        if (string.IsNullOrEmpty(Letters))
        {
            return string.Empty;
        }

        HashSet<char> Unique = [];
        foreach (char Character in Letters)
        {
            if (char.IsLetter(Character))
            {
                Unique.Add(char.ToLowerInvariant(Character));
            }
        }

        List<char> Ordered = new List<char>(Unique);
        Ordered.Sort();
        return new string([.. Ordered]);
    }
}
