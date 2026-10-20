using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

[assembly: CatalogueWatchdogAction]

/// <summary>Reads the single test/audit stress classification; none of these settings is an engine or rule limit.</summary>
internal static class CatalogueScenarioPolicy
{
    private static readonly JsonDocument Configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "CatalogueScenarioPolicy.json")));
    /// <summary>Normal per-scenario watchdog, shared with the PowerShell process supervisor.</summary>
    internal static int NormalWatchdogSeconds => Configuration.RootElement.GetProperty("normal_watchdog_seconds").GetInt32();
    /// <summary>Approved watchdog for the existing named stress scenarios only.</summary>
    internal static int StressWatchdogSeconds => Configuration.RootElement.GetProperty("stress_watchdog_seconds").GetInt32();
    /// <summary>The existing quality-excluded stress identities, with ordinal matching.</summary>
    internal static IReadOnlySet<string> StressScenarios { get; } = Configuration.RootElement.GetProperty("stress_scenarios")
        .EnumerateArray().Select(Item => Item.GetString()!).ToHashSet(StringComparer.Ordinal);

    /// <summary>Resolves the watchdog without affecting layout or quality metrics.</summary>
    /// <param name="Id">Catalogue identity, or null for an ordinary fixture.</param>
    /// <returns>The normal or approved stress duration in seconds.</returns>
    internal static int WatchdogSeconds(string? Id) => Id != null && StressScenarios.Contains(Id) ? StressWatchdogSeconds : NormalWatchdogSeconds;

    /// <summary>Adds discovery-visible watchdog metadata without changing a case name or assertion.</summary>
    /// <param name="Case">Existing case and arguments.</param>
    /// <param name="Id">Original catalogue identity.</param>
    /// <returns>The same case with its test-only timeout, description and classification.</returns>
    internal static TestCaseData Apply(TestCaseData Case, string Id)
    {
        Case.SetProperty("CatalogueScenarioId", Id).SetProperty("CatalogueWatchdogSeconds", WatchdogSeconds(Id));
        if (StressScenarios.Contains(Id)) Case.SetCategory("Stress").SetProperty("Description", $"Stress watchdog: {StressWatchdogSeconds} seconds; excluded from quality totals.");
        return Case;
    }

    /// <summary>Identifies typed catalogue arguments first, then named fixed-scenario fixtures.</summary>
    /// <param name="Test">Actual NUnit case, including its resolved arguments.</param>
    /// <returns>A named stress identity where present, otherwise the ordinary identity or null.</returns>
    internal static string? Identify(ITest Test)
    {
        if (Test.Properties.Get("CatalogueScenarioId") is string Declared) return Declared;
        if (Test.Arguments.Length >= 2 && Test.Arguments[0] is string Prefix && Prefix is "A" or "F" or "EIP" && Test.Arguments[1] is int Number)
            return Prefix + Number.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var Argument in Test.Arguments)
            if (Argument?.GetType().GetProperty("Id")?.GetValue(Argument) is string Id) return Id;
        var Names = Regex.Matches(Test.Name, @"(?<![A-Za-z0-9])(?:EIP|A|F)[0-9]+(?![0-9])").Select(Match => Match.Value).ToArray();
        return Names.FirstOrDefault(StressScenarios.Contains) ?? Names.FirstOrDefault();
    }
}

/// <summary>Publishes immediate per-test progress for an external process watchdog, which can actually stop a non-cooperative synchronous search on modern .NET.</summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CatalogueWatchdogActionAttribute : Attribute, ITestAction
{
    /// <summary>Applies only to individual cases, not to a whole fixture or catalogue batch.</summary>
    public ActionTargets Targets => ActionTargets.Test;
    /// <summary>Reports the case's policy before setup and layout can spend time in the engine.</summary>
    /// <param name="Test">Current resolved NUnit case.</param>
    public void BeforeTest(ITest Test)
    {
        string? Id = CatalogueScenarioPolicy.Identify(Test);
        int Seconds = CatalogueScenarioPolicy.WatchdogSeconds(Id);
        if (Id != null)
            TestContext.Progress.WriteLine($"CATALOGUE_WATCHDOG|{Id}|seconds={Seconds}|stress={CatalogueScenarioPolicy.StressScenarios.Contains(Id)}");
        if (Id != null && CatalogueScenarioPolicy.StressScenarios.Contains(Id))
            Test.Properties.Set("Description", $"Stress watchdog: {Seconds} seconds; excluded from quality totals.");
        Publish(Test, Id, false);
    }
    /// <summary>Marks completion so time between cases cannot inherit a stress allowance.</summary>
    /// <param name="Test">Case that has finished, whether passing or failing.</param>
    public void AfterTest(ITest Test) => Publish(Test, CatalogueScenarioPolicy.Identify(Test), true);
    /// <summary>Writes a small process-local progress record without modifying any engine state.</summary>
    /// <param name="Test">Actual case identity.</param>
    /// <param name="Id">Catalogue scenario if identifiable.</param>
    /// <param name="Completed">Whether the case has finished.</param>
    private static void Publish(ITest Test, string? Id, bool Completed)
    {
        string? Path = Environment.GetEnvironmentVariable("CATALOGUE_WATCHDOG_PROGRESS");
        if (string.IsNullOrWhiteSpace(Path)) return;
        File.WriteAllText(Path, JsonSerializer.Serialize(new { TestId = Test.Id, Test.FullName, Scenario = Id,
            Completed, UnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }));
    }
}
