using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace LiveEditor.Archi;

/// <summary>
/// Builds stable Archi object ids and Mermaid identifiers for round-trips.
/// </summary>
internal static class ArchiObjectIds
{
    private static readonly Regex ValidMermaidIdRegex = new Regex(
        @"^[\p{L}\p{N}_][\p{L}\p{M}\p{N}_.-]*$",
        RegexOptions.Compiled);

    private static readonly Regex ArchiHexIdRegex = new Regex(
        @"^id-[0-9a-fA-F]{32}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns a stable Archi id derived from a Mermaid identifier.
    /// Element ids that are already valid Mermaid ids use an <c>id-m-</c> prefix so round-trips keep the same id.
    /// </summary>
    /// <param name="Kind">Object kind prefix such as model, element, or relationship.</param>
    /// <param name="MermaidKey">Stable Mermaid key.</param>
    /// <returns>Archi-style id.</returns>
    public static string FromMermaidKey(string Kind, string MermaidKey)
    {
        if (string.Equals(Kind, "element", StringComparison.Ordinal)
            && IsValidMermaidId(MermaidKey))
        {
            return "id-m-" + MermaidKey;
        }

        string Material = (Kind ?? string.Empty) + ":" + (MermaidKey ?? string.Empty);
        byte[] Hash = MD5.HashData(Encoding.UTF8.GetBytes(Material));
        StringBuilder Builder = new StringBuilder(35);
        Builder.Append("id-");
        for (int Index = 0; Index < Hash.Length; Index++)
        {
            Builder.Append(Hash[Index].ToString("x2", CultureInfo.InvariantCulture));
        }

        return Builder.ToString();
    }

    /// <summary>
    /// Returns true when the value is a usable Mermaid enterprise identifier.
    /// </summary>
    /// <param name="Candidate">Candidate id.</param>
    /// <returns>True when valid.</returns>
    public static bool IsValidMermaidId(string? Candidate)
    {
        if (string.IsNullOrWhiteSpace(Candidate))
        {
            return false;
        }

        return ValidMermaidIdRegex.IsMatch(Candidate);
    }

    /// <summary>
    /// Builds a Mermaid id from an Archi id and display name.
    /// </summary>
    /// <param name="ArchiId">Archi object id.</param>
    /// <param name="Name">Display name.</param>
    /// <param name="UsedIds">Ids already taken.</param>
    /// <returns>Unique Mermaid id.</returns>
    public static string ToMermaidId(string ArchiId, string Name, HashSet<string> UsedIds)
    {
        if (TryExtractEmbeddedMermaidId(ArchiId, out string Embedded) && !UsedIds.Contains(Embedded))
        {
            UsedIds.Add(Embedded);
            return Embedded;
        }

        string Base = SanitiseName(Name);
        if (string.IsNullOrEmpty(Base))
        {
            Base = "element";
        }

        string Candidate = Base;
        int Suffix = 2;
        while (UsedIds.Contains(Candidate) || !IsValidMermaidId(Candidate))
        {
            Candidate = Base + Suffix.ToString(CultureInfo.InvariantCulture);
            Suffix++;
        }

        UsedIds.Add(Candidate);
        return Candidate;
    }

    /// <summary>
    /// Returns true when the id looks like a generated Archi hex id.
    /// </summary>
    /// <param name="ArchiId">Candidate id.</param>
    /// <returns>True when it matches id- plus 32 hex digits.</returns>
    public static bool LooksLikeArchiHexId(string? ArchiId)
    {
        if (string.IsNullOrWhiteSpace(ArchiId))
        {
            return false;
        }

        return ArchiHexIdRegex.IsMatch(ArchiId);
    }

    private static bool TryExtractEmbeddedMermaidId(string ArchiId, out string MermaidId)
    {
        MermaidId = string.Empty;
        const string Prefix = "id-m-";
        if (ArchiId.StartsWith(Prefix, StringComparison.Ordinal) && IsValidMermaidId(ArchiId.Substring(Prefix.Length)))
        {
            MermaidId = ArchiId.Substring(Prefix.Length);
            return true;
        }

        return false;
    }

    private static string SanitiseName(string Name)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return string.Empty;
        }

        StringBuilder Builder = new StringBuilder();
        foreach (char Character in Name.Trim())
        {
            if (char.IsLetterOrDigit(Character) || Character == '_' || Character == '-' || Character == '.')
            {
                Builder.Append(Character);
            }
            else if (char.IsWhiteSpace(Character) || Character == '/')
            {
                if (Builder.Length > 0 && Builder[^1] != '_')
                {
                    Builder.Append('_');
                }
            }
        }

        string Result = Builder.ToString().Trim('_', '-', '.');
        if (Result.Length > 0 && (Result[0] == '.' || Result[0] == '-'))
        {
            Result = "e" + Result;
        }

        return Result;
    }
}
