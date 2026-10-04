using System;
using System.Collections.Generic;
using System.IO;
using MDConverter;

namespace NativeCompareDocx;

internal static class Program
{
    private static int Main(string[] Args)
    {
        string OutputDocxPath;
        string WorkDirectory;

        if (Args.Length >= 1 && !Args[0].StartsWith('-'))
        {
            OutputDocxPath = Path.GetFullPath(Args[0]);
            WorkDirectory = Args.Length >= 2 && !Args[1].StartsWith('-')
                ? Path.GetFullPath(Args[1])
                : Path.Combine(Path.GetDirectoryName(OutputDocxPath) ?? ".", "artefacts");
        }
        else
        {
            string RepositoryRoot = FindRepositoryRoot();
            OutputDocxPath = Path.Combine(RepositoryRoot, "out", "r1-native-compare.docx");
            WorkDirectory = Path.Combine(RepositoryRoot, "out", "r1-native-compare");
        }

        try
        {
            List<MermaidCompareScenario> Scenarios = BuildR1Scenarios(FindRepositoryRoot());
            MermaidCompareDocxGenerator.Write(Scenarios, OutputDocxPath, WorkDirectory);
            Console.WriteLine("Wrote comparison DOCX: " + OutputDocxPath);
            Console.WriteLine("Artefacts: " + WorkDirectory);
            return 0;
        }
        catch (Exception Ex)
        {
            Console.Error.WriteLine("Failed: " + Ex.Message);
            return 1;
        }
    }

    private static List<MermaidCompareScenario> BuildR1Scenarios(string RepositoryRoot)
    {
        List<MermaidCompareScenario> Scenarios = [];
        string FixtureDirectory = Path.Combine(
            RepositoryRoot,
            "tests",
            "MermaidRecognition.Tests",
            "FlowchartRecognition");

        AddFixtureScenario(Scenarios, FixtureDirectory, "chain-a-b-c.mmd", "R1 — Chain A-B-C");
        AddFixtureScenario(Scenarios, FixtureDirectory, "subgraph-membership.mmd", "R1 — Subgraph membership");

        Scenarios.Add(new MermaidCompareScenario(
            "R1 — Decision fan-out",
            """
            flowchart TD
                Choose{Choose}
                Choose -->|Yes| Approve[Approved]
                Choose -->|No| Reject[Rejected]
            """));

        Scenarios.Add(new MermaidCompareScenario(
            "F02 — Flowchart LR chain",
            """
            flowchart LR
                Start[Start] --> Middle[Middle] --> End[End]
            """));

        return Scenarios;
    }

    private static void AddFixtureScenario(
        List<MermaidCompareScenario> Scenarios,
        string FixtureDirectory,
        string FileName,
        string Title)
    {
        string FixturePath = Path.Combine(FixtureDirectory, FileName);
        if (!File.Exists(FixturePath))
        {
            throw new FileNotFoundException("Fixture not found: " + FixturePath);
        }

        Scenarios.Add(new MermaidCompareScenario(Title, File.ReadAllText(FixturePath)));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
        while (Current != null)
        {
            if (File.Exists(Path.Combine(Current.FullName, "MDConverter.slnx"))
                || File.Exists(Path.Combine(Current.FullName, "spec.md")))
            {
                return Current.FullName;
            }

            Current = Current.Parent;
        }

        throw new InvalidOperationException("Could not locate MarkdownToDocX repository root.");
    }
}
