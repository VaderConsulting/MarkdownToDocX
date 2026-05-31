using MermaidRecognition;

namespace MermaidRecognition.Tests;

public class MermaidJsonConfigFlattenerTests
{
    [Test]
    public void ParseLeadingInitBlocks_FlattensNestedFlowchartKeys()
    {
        const string Input =
            """
            %%{init: {"flowchart": {"nodeSpacing": 80, "rankSpacing": 60}, "theme": "forest"}}%%
            flowchart LR
                A --> B
            """;

        Dictionary<string, string> Values = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(Input);

        Assert.That(Values["flowchart.nodeSpacing"], Is.EqualTo("80"));
        Assert.That(Values["flowchart.rankSpacing"], Is.EqualTo("60"));
        Assert.That(Values["theme"], Is.EqualTo("forest"));
    }

    [Test]
    public void ParseLeadingInitBlocks_MergesMultipleInitBlocks()
    {
        const string Input =
            """
            %%{init: {"flowchart": {"nodeSpacing": 70}}}%%
            %%{init: {"look": "neo"}}%%
            flowchart TD
                A --> B
            """;

        Dictionary<string, string> Values = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(Input);

        Assert.That(Values["flowchart.nodeSpacing"], Is.EqualTo("70"));
        Assert.That(Values["look"], Is.EqualTo("neo"));
    }

    [Test]
    public void ParseLeadingInitBlocks_AllowsWhitespaceBeforeWrapperClose()
    {
        const string Input =
            """
            %%{init: {"flowchart": {"htmlLabels": false}} }%%
            flowchart TB
            A["Native SVG labels"] --> B["Final"]
            """;

        Dictionary<string, string> Values = MermaidJsonConfigFlattener.ParseLeadingInitBlocks(Input);

        Assert.That(Values["flowchart.htmlLabels"], Is.EqualTo("false"));
    }
}

