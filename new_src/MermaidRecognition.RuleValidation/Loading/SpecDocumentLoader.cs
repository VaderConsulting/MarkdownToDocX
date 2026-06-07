using System.IO;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Json;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Loading;

/// <summary>
/// Loads the three specification JSON artefacts from a folder.
/// </summary>
public static class SpecDocumentLoader
{
    /// <summary>
    /// Loads <c>ruleset.json</c>, <c>evaluator-map.json</c>, and <c>expected-contract.json</c>.
    /// </summary>
    public static LoadedSpecBundle LoadFromDirectory(string SpecDirectory)
    {
        string RulesetPath = Path.Combine(SpecDirectory, "ruleset.json");
        string EvaluatorMapPath = Path.Combine(SpecDirectory, "evaluator-map.json");
        string ExpectedContractPath = Path.Combine(SpecDirectory, "expected-contract.json");

        RulesetDocument Ruleset = LoadJsonFile<RulesetDocument>(RulesetPath);
        EvaluatorMapDocument EvaluatorMap = LoadJsonFile<EvaluatorMapDocument>(EvaluatorMapPath);
        ExpectedContractDocument ExpectedContract = LoadJsonFile<ExpectedContractDocument>(ExpectedContractPath);

        return new LoadedSpecBundle
        {
            Ruleset = Ruleset,
            EvaluatorMap = EvaluatorMap,
            ExpectedContract = ExpectedContract,
            SpecDirectory = SpecDirectory
        };
    }

    private static TDocument LoadJsonFile<TDocument>(string FilePath)
    {
        string JsonText = File.ReadAllText(FilePath);
        TDocument? Document = JsonSerializer.Deserialize<TDocument>(JsonText, RuleValidationJsonOptions.Options);
        if (Document is null)
        {
            throw new InvalidDataException("Could not deserialise " + FilePath + ".");
        }

        return Document;
    }
}

/// <summary>
/// The three loaded specification artefacts.
/// </summary>
public sealed class LoadedSpecBundle
{
    public RulesetDocument Ruleset { get; set; } = new RulesetDocument();

    public EvaluatorMapDocument EvaluatorMap { get; set; } = new EvaluatorMapDocument();

    public ExpectedContractDocument ExpectedContract { get; set; } = new ExpectedContractDocument();

    public string SpecDirectory { get; set; } = string.Empty;
}
