using MermaidRecognition;

namespace MermaidRecognition.Tests;

public class DiagramConfigContractTests
{
    [Test]
    public void SetDefaultIfMissing_DoesNotOverrideExisting()
    {
        DiagramConfigContract Configuration = new DiagramConfigContract();
        Configuration.EffectiveValuesMutable["flowchart.nodeSpacing"] = "80";
        Configuration.ValueSourcesMutable["flowchart.nodeSpacing"] = ConfigValueSource.DirectiveOverride;

        Configuration.SetDefaultIfMissing("flowchart.nodeSpacing", "50");

        Assert.That(Configuration.EffectiveValues["flowchart.nodeSpacing"], Is.EqualTo("80"));
        Assert.That(Configuration.ValueSources["flowchart.nodeSpacing"], Is.EqualTo(ConfigValueSource.DirectiveOverride));
    }

    [Test]
    public void ApplyRawDirectivesToEffective_CopiesAllRawKeys()
    {
        DiagramConfigContract Configuration = new DiagramConfigContract();
        Configuration.RawDirectiveValuesMutable["flowchart.rankSpacing"] = "40";
        Configuration.RawDirectiveValuesMutable["theme"] = "forest";

        Configuration.ApplyRawDirectivesToEffective(ConfigValueSource.DirectiveOverride);

        Assert.That(Configuration.EffectiveValues["flowchart.rankSpacing"], Is.EqualTo("40"));
        Assert.That(Configuration.EffectiveValues["theme"], Is.EqualTo("forest"));
    }

    [Test]
    public void BuildEffectiveConfiguration_AppliesPinnedGlobalAndKindDefaults()
    {
        DiagramConfigContract Configuration = new DiagramConfigContract();
        Configuration.BuildEffectiveConfiguration("sequence");

        Assert.That(Configuration.EffectiveValues["theme"], Is.EqualTo("default"));
        Assert.That(Configuration.EffectiveValues["fontSize"], Is.EqualTo("16"));
        Assert.That(Configuration.EffectiveValues["useMaxWidth"], Is.EqualTo("true"));
        Assert.That(Configuration.EffectiveValues["sequence.diagramMarginX"], Is.EqualTo("50"));
        Assert.That(Configuration.ValueSources["theme"], Is.EqualTo(ConfigValueSource.Default));
    }

    [Test]
    public void BuildEffectiveConfiguration_DirectiveOverridesPinnedDefault()
    {
        DiagramConfigContract Configuration = new DiagramConfigContract();
        Configuration.RawDirectiveValuesMutable["flowchart.nodeSpacing"] = "80";
        Configuration.BuildEffectiveConfiguration("flowchart");

        Assert.That(Configuration.EffectiveValues["flowchart.nodeSpacing"], Is.EqualTo("80"));
        Assert.That(Configuration.ValueSources["flowchart.nodeSpacing"], Is.EqualTo(ConfigValueSource.DirectiveOverride));
        Assert.That(Configuration.EffectiveValues["flowchart.rankSpacing"], Is.EqualTo("50"));
    }
}

