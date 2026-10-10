using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Tests.Enterprise;

[TestFixture]
public sealed class EnterpriseConfigurationTests
{
    [Test]
    public void Frontmatter_PreservesTitleConfigurationAndOriginalLineNumbers()
    {
        var Result = EnterpriseParser.Parse("---\ntitle: Preliminary\nconfig:\n  unknown: ignored\n  theme: neutral\n---\nenterprise\napplication-component first\ninvalid input extra");
        Assert.That(Result.Document.Title, Is.EqualTo("Preliminary"));
        Assert.That(Result.Document.Configuration["theme"], Is.EqualTo("neutral"));
        Assert.That(Result.Document.Elements["first"].LineNumber, Is.EqualTo(8));
        Assert.That(Result.Diagnostics.Single().LineNumber, Is.EqualTo(9));
    }

    [Test]
    public void InitDirectives_AllowMultilineSettingsInsideDiagramAndLastValuesWin()
    {
        var Result = EnterpriseParser.Parse("%%{init: {\"theme\":\"forest\"}}%%\nenterprise\n%%{init: {\n\"themeVariables\": {\"fontSize\":\"14px\"}, \"theme\":\"neutral\"\n}}%%\napplication-component first");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Configuration["theme"], Is.EqualTo("neutral"));
        Assert.That(Result.Document.Configuration["themeVariables.fontSize"], Is.EqualTo("14px"));
        Assert.That(Result.Document.Elements["first"].LineNumber, Is.EqualTo(6));
    }

    [Test]
    public void ExplicitTitle_OverridesFrontmatterTitle()
    {
        var Result = EnterpriseParser.Parse("---\ntitle: Preliminary\n---\nenterprise\ntitle Final\napplication-component first");
        Assert.That(Result.HasErrors, Is.False);
        Assert.That(Result.Document.Title, Is.EqualTo("Final"));
    }
}
