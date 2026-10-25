using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseStyleTests
{
    [Test]
    public void ClassesAndInlineStyles_ResolveForwardReferencesAndIgnoreUndefinedClasses()
    {
        var Result = EnterpriseParser.ParseDocument("enterprise\nclass first accent\napplication-component first:::extra 'First'\napplication-service second 'Second':::undefined\nfirst -realization-> second:::accent\nclassDef default stroke-width:2px\nclassDef accent fill:#abcdef\nclassDef extra opacity:0.8\nstyle first stroke:#123456");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Diagnostics, Is.Empty);
        Assert.That(Result.Document.Styles.Elements["first"]["fill"], Is.EqualTo("#abcdef"));
        Assert.That(Result.Document.Styles.Elements["first"]["opacity"], Is.EqualTo("0.8"));
        Assert.That(Result.Document.Styles.Elements["second"]["stroke-width"], Is.EqualTo("2px"));
    }

    [Test]
    public void UnknownStyleTargets_GiveWarningsAndInvalidLinkIndicesGiveOneError()
    {
        var Result = EnterpriseParser.ParseDocument("enterprise\napplication-component first\nclass first,missing undefined\nstyle absent fill:red\nlinkStyle 0,3 stroke:blue");
        Assert.That(Result.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Warning), Is.EqualTo(2));
        Assert.That(Result.Diagnostics.Count(Item => Item.Severity == MermaidDiagnosticSeverity.Error), Is.EqualTo(1));
        Assert.That(Result.Document.Elements, Has.Count.EqualTo(1));
    }

    [Test]
    public void LinkStyles_PreserveEscapedCommaAndOptionalInterpolation()
    {
        var Result = EnterpriseParser.ParseDocument("enterprise; application-component first; application-service second; first -realization-> second; linkStyle default stroke:gray; linkStyle 0 interpolate basis stroke:blue,stroke-dasharray:5\\,5,opacity:0.6");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Styles.DefaultLink["stroke"], Is.EqualTo("gray"));
        Assert.That(Result.Document.Styles.Links[0]["stroke-dasharray"], Is.EqualTo("5,5"));
        Assert.That(Result.Document.Styles.Links[0]["opacity"], Is.EqualTo("0.6"));
    }
}
