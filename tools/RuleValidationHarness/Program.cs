using System;
using System.Globalization;
using System.IO;
using MermaidRecognition.RuleValidation.Models;
using MermaidRecognition.RuleValidation.Pipeline;

namespace RuleValidationHarness;

/// <summary>
/// CLI entry point for Markdown rule-validation runs.
/// </summary>
internal static class Program
{
    private static int Main(string[] Args)
    {
        if (Args.Length < 2)
        {
            Console.WriteLine("Usage: RuleValidationHarness <markdown-file> <output-directory> [spec-directory]");
            return 1;
        }

        string MarkdownFilePath = Path.GetFullPath(Args[0]);
        string OutputDirectory = Path.GetFullPath(Args[1]);
        string SpecDirectory = Args.Length >= 3
            ? Path.GetFullPath(Args[2])
            : ResolveDefaultSpecDirectory();

        if (!File.Exists(MarkdownFilePath))
        {
            Console.WriteLine("Markdown file not found: " + MarkdownFilePath);
            return 1;
        }

        if (!Directory.Exists(SpecDirectory))
        {
            Console.WriteLine("Specification directory not found: " + SpecDirectory);
            return 1;
        }

        MermaidRuleValidationPipeline Pipeline = new MermaidRuleValidationPipeline(SpecDirectory);
        MermaidRuleValidationPipelineResult Result = Pipeline.ProcessMarkdownFile(MarkdownFilePath, OutputDirectory);

        Console.WriteLine("RunId: " + Result.ActualResults.RunId);
        Console.WriteLine("Diagrams: " + Result.ActualResults.Diagrams.Count.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("Overall result: " + Result.ValidationReport.OverallResult);
        Console.WriteLine("Errors: " + Result.ValidationReport.Summary.ErrorCount.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("Warnings: " + Result.ValidationReport.Summary.WarningCount.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("Actual results: " + Result.ActualResultsPath);
        Console.WriteLine("Validation report: " + Result.ValidationReportPath);

        if (Result.ValidationReport.OverallResult == "Fail")
        {
            int Shown = 0;
            foreach (ValidationFailureRecord Failure in Result.ValidationReport.Failures)
            {
                if (Failure.Severity != "Error")
                {
                    continue;
                }

                Console.WriteLine(Failure.Message);
                Shown++;
                if (Shown >= 20)
                {
                    break;
                }
            }

            return 2;
        }

        return 0;
    }

    private static string ResolveDefaultSpecDirectory()
    {
        string Current = AppContext.BaseDirectory;
        for (int Depth = 0; Depth < 8; Depth++)
        {
            string Candidate = Path.Combine(Current, "Rule Specification");
            if (Directory.Exists(Candidate)
                && File.Exists(Path.Combine(Candidate, "ruleset.json"))
                && File.Exists(Path.Combine(Candidate, "evaluator-map.json"))
                && File.Exists(Path.Combine(Candidate, "expected-contract.json")))
            {
                return Candidate;
            }

            DirectoryInfo? Parent = Directory.GetParent(Current);
            if (Parent is null)
            {
                break;
            }

            Current = Parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate Rule Specification directory.");
    }
}
