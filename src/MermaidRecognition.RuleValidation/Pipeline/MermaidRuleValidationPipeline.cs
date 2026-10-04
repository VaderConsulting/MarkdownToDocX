using System;
using System.IO;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Engine;
using MermaidRecognition.RuleValidation.Evaluators;
using MermaidRecognition.RuleValidation.Generation;
using MermaidRecognition.RuleValidation.Json;
using MermaidRecognition.RuleValidation.Loading;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Pipeline;

/// <summary>
/// End-to-end Markdown to validation-report pipeline.
/// </summary>
public sealed class MermaidRuleValidationPipeline
{
    private readonly LoadedSpecBundle _SpecBundle;

    private readonly RulesEngine _RulesEngine;

    /// <summary>
    /// Creates a pipeline using specification artefacts from a directory.
    /// </summary>
    public MermaidRuleValidationPipeline(string SpecDirectory)
    {
        _SpecBundle = SpecDocumentLoader.LoadFromDirectory(SpecDirectory);
        RequireFactEvaluator RequireFactEvaluator = new RequireFactEvaluator();
        _RulesEngine = new RulesEngine(_SpecBundle, new IRuleEvaluator[] { RequireFactEvaluator });
    }

    /// <summary>
    /// Processes one Markdown file and writes actual-results and validation-report JSON files.
    /// </summary>
    public MermaidRuleValidationPipelineResult ProcessMarkdownFile(
        string MarkdownFilePath,
        string OutputDirectory)
    {
        string Markdown = File.ReadAllText(MarkdownFilePath);
        string RunId = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        ActualResultsDocument ActualResults = ActualResultsGenerator.GenerateFromMarkdown(
            Markdown,
            MarkdownFilePath,
            RunId);

        ValidationReportDocument ValidationReport = _RulesEngine.Validate(ActualResults);

        Directory.CreateDirectory(OutputDirectory);
        string ActualResultsPath = Path.Combine(OutputDirectory, "actual-results.json");
        string ValidationReportPath = Path.Combine(OutputDirectory, "validation-report.json");
        WriteJson(ActualResultsPath, ActualResults);
        WriteJson(ValidationReportPath, ValidationReport);

        return new MermaidRuleValidationPipelineResult
        {
            ActualResults = ActualResults,
            ValidationReport = ValidationReport,
            ActualResultsPath = ActualResultsPath,
            ValidationReportPath = ValidationReportPath
        };
    }

    private static void WriteJson<TDocument>(string FilePath, TDocument Document)
    {
        string JsonText = JsonSerializer.Serialize(Document, RuleValidationJsonOptions.Options);
        File.WriteAllText(FilePath, JsonText);
    }
}

/// <summary>
/// Output from one pipeline run.
/// </summary>
public sealed class MermaidRuleValidationPipelineResult
{
    public ActualResultsDocument ActualResults { get; set; } = new ActualResultsDocument();

    public ValidationReportDocument ValidationReport { get; set; } = new ValidationReportDocument();

    public string ActualResultsPath { get; set; } = string.Empty;

    public string ValidationReportPath { get; set; } = string.Empty;
}
