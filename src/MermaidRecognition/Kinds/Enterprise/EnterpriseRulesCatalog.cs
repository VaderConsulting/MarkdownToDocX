using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MermaidRecognition.Kinds.Enterprise;

/// <summary>
/// Embedded Application + Technology relationship tables for ArchiMate 3.1 and 3.2.
/// </summary>
public sealed class EnterpriseRulesCatalog
{
    private const string RESOURCE_NAME = "MermaidRecognition.Enterprise.enterprise-rules.json";

    private static readonly Lazy<EnterpriseRulesCatalog> _Instance = new Lazy<EnterpriseRulesCatalog>(LoadEmbedded);

    private readonly Dictionary<string, string[][]> _Matrices;

    private readonly List<string> _Types;

    private readonly HashSet<string> _ProvisionalKeys;

    /// <summary>
    /// Creates a catalog from already-deserialised tables.
    /// </summary>
    /// <param name="Matrices">Version to letter-matrix map.</param>
    /// <param name="Types">Row/column type order matching the matrix.</param>
    /// <param name="ProvisionalKeys">source|target keys that differ in 3.2.</param>
    public EnterpriseRulesCatalog(
        Dictionary<string, string[][]> Matrices,
        List<string> Types,
        HashSet<string> ProvisionalKeys)
    {
        _Matrices = Matrices ?? new Dictionary<string, string[][]>(StringComparer.Ordinal);
        _Types = Types ?? new List<string>();
        _ProvisionalKeys = ProvisionalKeys ?? new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>Gets the catalog loaded from the embedded rules JSON.</summary>
    public static EnterpriseRulesCatalog Instance
    {
        get
        {
            return _Instance.Value;
        }
    }

    /// <summary>Gets the type order used by the matrices.</summary>
    public IReadOnlyList<string> Types
    {
        get
        {
            return _Types;
        }
    }

    /// <summary>
    /// Returns the allowed relationship letters for a source/target pair.
    /// </summary>
    /// <param name="Version">Rule-table version.</param>
    /// <param name="SourceType">Kebab-case source type.</param>
    /// <param name="TargetType">Kebab-case target type.</param>
    /// <returns>Allowed letters, or empty when the pair is unknown.</returns>
    public string GetAllowedLetters(string Version, string SourceType, string TargetType)
    {
        string TableVersion = string.IsNullOrWhiteSpace(Version) ? EnterpriseParser.DEFAULT_VERSION : Version;
        if (!_Matrices.TryGetValue(TableVersion, out string[][]? Matrix) || Matrix == null)
        {
            return string.Empty;
        }

        int SourceIndex = _Types.IndexOf(SourceType);
        int TargetIndex = _Types.IndexOf(TargetType);
        if (SourceIndex < 0 || TargetIndex < 0 || SourceIndex >= Matrix.Length)
        {
            return string.Empty;
        }

        string[] Row = Matrix[SourceIndex];
        if (TargetIndex >= Row.Length)
        {
            return string.Empty;
        }

        return Row[TargetIndex] ?? string.Empty;
    }

    /// <summary>
    /// Returns true when the pair is one of the 15 unverified 3.2 cells.
    /// </summary>
    /// <param name="SourceType">Kebab-case source type.</param>
    /// <param name="TargetType">Kebab-case target type.</param>
    /// <returns>True when the cell is provisional.</returns>
    public bool IsProvisionalCell(string SourceType, string TargetType)
    {
        return _ProvisionalKeys.Contains(SourceType + "|" + TargetType);
    }

    private static EnterpriseRulesCatalog LoadEmbedded()
    {
        Assembly CatalogAssembly = typeof(EnterpriseRulesCatalog).Assembly;
        using Stream? ResourceStream = CatalogAssembly.GetManifestResourceStream(RESOURCE_NAME);
        if (ResourceStream == null)
        {
            throw new InvalidOperationException("Embedded enterprise rules resource was not found: " + RESOURCE_NAME);
        }

        using StreamReader Reader = new StreamReader(ResourceStream);
        string Json = Reader.ReadToEnd();
        return LoadJson(Json);
    }

    /// <summary>
    /// Loads a catalog from JSON text (used by tests and the embedded resource).
    /// </summary>
    /// <param name="Json">Rules JSON.</param>
    /// <returns>Loaded catalog.</returns>
    public static EnterpriseRulesCatalog LoadJson(string Json)
    {
        JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        EnterpriseRulesJsonFile? File = JsonSerializer.Deserialize<EnterpriseRulesJsonFile>(Json, Options);
        if (File == null || File.Types == null || File.Versions == null)
        {
            throw new InvalidOperationException("enterprise-rules.json is missing types or versions.");
        }

        HashSet<string> ProvisionalKeys = new HashSet<string>(StringComparer.Ordinal);
        if (File.Provisional?.Cells != null)
        {
            foreach (EnterpriseProvisionalCellJson Cell in File.Provisional.Cells)
            {
                if (!string.IsNullOrEmpty(Cell.Source) && !string.IsNullOrEmpty(Cell.Target))
                {
                    ProvisionalKeys.Add(Cell.Source + "|" + Cell.Target);
                }
            }
        }

        Dictionary<string, string[][]> Matrices = new Dictionary<string, string[][]>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string[][]> Entry in File.Versions)
        {
            Matrices[Entry.Key] = Entry.Value;
        }

        return new EnterpriseRulesCatalog(Matrices, File.Types, ProvisionalKeys);
    }

    private sealed class EnterpriseRulesJsonFile
    {
        public List<string>? Types { get; set; }

        public Dictionary<string, string[][]>? Versions { get; set; }

        public EnterpriseProvisionalJson? Provisional { get; set; }
    }

    private sealed class EnterpriseProvisionalJson
    {
        public List<EnterpriseProvisionalCellJson>? Cells { get; set; }
    }

    private sealed class EnterpriseProvisionalCellJson
    {
        public string? Source { get; set; }

        public string? Target { get; set; }

        [JsonPropertyName("v3_1")]
        public string? V31 { get; set; }

        [JsonPropertyName("v3_2")]
        public string? V32 { get; set; }
    }
}
