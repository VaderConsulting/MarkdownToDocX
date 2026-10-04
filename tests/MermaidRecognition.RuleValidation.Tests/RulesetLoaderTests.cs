using NUnit.Framework;
using System.Text.Json;
using MermaidRecognition.RuleValidation.Json;
using MermaidRecognition.RuleValidation.Loading;
using MermaidRecognition.RuleValidation.Models;

namespace MermaidRecognition.RuleValidation.Tests;

[TestFixture]
public sealed class RulesetLoaderTests
{
    [Test]
    public void Ruleset_RequireDeserializesForHeaderGraphAlias()
    {
        string SpecDirectory = Path.Combine(AppContext.BaseDirectory, "Rule Specification");
        LoadedSpecBundle Bundle = SpecDocumentLoader.LoadFromDirectory(SpecDirectory);
        RuleDefinition? Rule = Bundle.Ruleset.Rules.FirstOrDefault(
            Candidate => Candidate.RuleId == "Header.GraphAlias");
        Assert.That(Rule, Is.Not.Null);
        Assert.That(Rule!.Require.Count, Is.GreaterThan(0));
        Assert.That(Rule.Require.ContainsKey("LegacyCatalogueRuleSatisfied"), Is.True);
    }
}
