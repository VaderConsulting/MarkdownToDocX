using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseValidatorTests
{
    [Test]
    public void Analyze_InvalidRelationshipsSample_ReportsExactlyFiveErrorsOnLines12To16()
    {
        EnterpriseAnalysisResult Result = EnterpriseValidator.Analyze(
            EnterpriseSamplePaths.ReadSample("04-invalid-relationships.mmd"));

        Assert.That(Result.Errors, Has.Count.EqualTo(5), string.Join(Environment.NewLine, Result.Errors.Select(Item => Item.Message)));
        Assert.That(Result.Errors.Select(Item => Item.LineNumber).ToArray(), Is.EqualTo([12, 13, 14, 15, 16]));
        Assert.That(
            Result.Errors[0].Message,
            Is.EqualTo("line 12: serving from DataObject \"Order\" to ApplicationComponent \"Web Shop\" is not allowed in Enterprise 3.2. Allowed: association."));
        Assert.That(
            Result.Errors[1].Message,
            Is.EqualTo("line 13: composition from ApplicationService \"Online Ordering\" to ApplicationComponent \"Web Shop\" is not allowed in Enterprise 3.2. Allowed: flow, association, triggering, serving."));
        Assert.That(
            Result.Errors[2].Message,
            Is.EqualTo("line 14: realization from ApplicationComponent \"Web Shop\" to Node \"App Server Node\" is not allowed in Enterprise 3.2. Allowed: flow, association, triggering, serving."));
        Assert.That(
            Result.Errors[3].Message,
            Is.EqualTo("line 15: assignment from Artifact \"shop.war\" to ApplicationComponent \"Web Shop\" is not allowed in Enterprise 3.2. Allowed: association, realization."));
        Assert.That(
            Result.Errors[4].Message,
            Is.EqualTo("line 16: access from TechnologyService \"Hosting\" to Node \"App Server Node\" is not allowed in Enterprise 3.2. Allowed: flow, association, triggering, serving."));
    }

    [Test]
    public void Analyze_Version32Delta_RejectsDeviceRealizesNodeWithProvisionalNote()
    {
        EnterpriseAnalysisResult Result = EnterpriseValidator.Analyze(
            EnterpriseSamplePaths.ReadSample("05-version-3.2-delta.mmd"));

        Assert.That(Result.Errors, Has.Count.EqualTo(1), string.Join(Environment.NewLine, Result.Errors.Select(Item => Item.Message)));
        Assert.That(Result.Errors[0].LineNumber, Is.EqualTo(6));
        Assert.That(Result.Errors[0].Message, Does.Contain("realization from Device \"Rack Server\" to Node \"Web Node\" is not allowed in Enterprise 3.2."));
        Assert.That(Result.Errors[0].Message, Does.Contain("Allowed: flow, association, triggering, serving."));
        Assert.That(Result.Errors[0].Message, Does.Contain(EnterpriseValidator.PROVISIONAL_NOTE));
    }

    [Test]
    public void Analyze_Version32DeltaForcedTo31_IsValid()
    {
        string Source = EnterpriseSamplePaths.ReadSample("05-version-3.2-delta.mmd")
            .Replace("version 3.2", "version 3.1", StringComparison.Ordinal);
        EnterpriseAnalysisResult Result = EnterpriseValidator.Analyze(Source);
        Assert.That(Result.HasErrors, Is.False, string.Join(Environment.NewLine, Result.Errors.Select(Item => Item.Message)));
    }

    [TestCase("01-application-layer.mmd")]
    [TestCase("02-technology-layer.mmd")]
    [TestCase("03-cross-layer-webshop.mmd")]
    [TestCase("06-business-layer.mmd")]
    [TestCase("07-motivation.mmd")]
    [TestCase("08-strategy.mmd")]
    [TestCase("09-physical.mmd")]
    [TestCase("10-implementation-migration.mmd")]
    [TestCase("11-grouping-location-junction.mmd")]
    [TestCase("12-motivation-to-technology.mmd")]
    public void Analyze_ValidSamples_AreValidUnder31And32(string FileName)
    {
        string Source = EnterpriseSamplePaths.ReadSample(FileName);
        EnterpriseAnalysisResult DefaultResult = EnterpriseValidator.Analyze(Source);
        Assert.That(DefaultResult.HasErrors, Is.False, FileName + " default: " + string.Join("; ", DefaultResult.Errors.Select(Item => Item.Message)));

        string Forced31 = EnsureVersion(Source, "3.1");
        string Forced32 = EnsureVersion(Source, "3.2");
        EnterpriseAnalysisResult Result31 = EnterpriseValidator.Analyze(Forced31);
        EnterpriseAnalysisResult Result32 = EnterpriseValidator.Analyze(Forced32);
        Assert.That(Result31.HasErrors, Is.False, FileName + " 3.1: " + string.Join("; ", Result31.Errors.Select(Item => Item.Message)));
        Assert.That(Result32.HasErrors, Is.False, FileName + " 3.2: " + string.Join("; ", Result32.Errors.Select(Item => Item.Message)));
    }

    [Test]
    public void Validate_NestedElementWithoutRelationship_WarnsAndDoesNotHideUnrelatedEdges()
    {
        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "Web Shop"
              application-function checkout "Checkout" in webShop
              data-object order "Order"
              checkout -access(write)-> order
            """);

        Assert.That(Parsed.HasErrors, Is.False);
        List<EnterpriseDiagnostic> Diagnostics = EnterpriseValidator.Validate(Parsed.Document, null);
        Assert.That(Diagnostics.Any(Item => Item.Severity == MermaidDiagnosticSeverity.Error), Is.False);
        EnterpriseDiagnostic Warning = Diagnostics.Single(Item => Item.Severity == MermaidDiagnosticSeverity.Warning);
        Assert.That(Warning.LineNumber, Is.EqualTo(3));
        Assert.That(Warning.Message, Does.Contain("nested in element \"webShop\""));
        Assert.That(Parsed.Document.Relationships[0].HiddenByNesting, Is.False);
    }

    [Test]
    public void Validate_NestedAssignment_HidesTheNestingRelationship()
    {
        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(
            """
            enterprise
              application-component webShop "Web Shop"
              application-function checkout "Checkout" in webShop
              webShop -assignment-> checkout
            """);

        List<EnterpriseDiagnostic> Diagnostics = EnterpriseValidator.Validate(Parsed.Document, null);
        Assert.That(Diagnostics.Any(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.False);
        Assert.That(Parsed.Document.Relationships[0].HiddenByNesting, Is.True);
    }

    [Test]
    public void Validate_NestedInGrouping_DoesNotWarn()
    {
        EnterpriseParseResult Parsed = EnterpriseParser.ParseDocument(
            """
            enterprise
              group store "Store"
              business-actor clerk "Clerk" in store
            """);

        Assert.That(Parsed.HasErrors, Is.False);
        List<EnterpriseDiagnostic> Diagnostics = EnterpriseValidator.Validate(Parsed.Document, null);
        Assert.That(Diagnostics.Any(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.False);
    }

    [Test]
    public void IsProvisionalCell_NewLayerType_IsProvisional()
    {
        Assert.That(EnterpriseRulesCatalog.Instance.IsProvisionalCell("business-actor", "business-role"), Is.True);
        Assert.That(EnterpriseRulesCatalog.Instance.IsProvisionalCell("application-component", "data-object"), Is.False);
    }

    private static string EnsureVersion(string Source, string Version)
    {
        if (Source.Contains("version ", StringComparison.Ordinal))
        {
            return System.Text.RegularExpressions.Regex.Replace(Source, @"version\s+3\.[12]", "version " + Version);
        }

        return Source.Replace("enterprise", "enterprise" + Environment.NewLine + "  version " + Version, StringComparison.Ordinal);
    }
}
