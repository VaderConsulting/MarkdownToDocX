using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Flowchart;

namespace MermaidRecognition.Tests.Flowchart;

/// <summary>
/// Regenerates §11.11 verified JSON fixtures (run explicitly when parser output is intentional).
/// </summary>
public class FlowchartRecognitionFixtureWriter
{
    [Test]
    [Explicit("Regenerates baseline fixtures; select this test explicitly to update them.")]
    public void Write_All_Recognition_Fixtures()
    {
        string BaseDir = GetFixtureDirectory();
        Directory.CreateDirectory(BaseDir);

        WriteFixture(
            BaseDir,
            "edge-identity",
            """
            flowchart TD
                A & B e1@--> C & D
            """);

        WriteFixture(
            BaseDir,
            "metadata-shape-data",
            """
            flowchart TD
                Doc@{ shape: doc }
                Store[(Data Store)]
            """);

        WriteFixture(
            BaseDir,
            "node-bracket-shapes",
            """
            flowchart TD
                Rect[Rectangle]
                Round(Rounded)
                Diamond{Decision}
                Circle((Terminal))
                Stadium([Stadium])
                Sub[[Subroutine]]
                Cyl[(Cylinder)]
            """);

        WriteFixture(
            BaseDir,
            "subgraph-membership",
            """
            flowchart TD
                subgraph Inner [Inner title]
                    X --> Y
                end
            """);

        WriteFixture(
            BaseDir,
            "config-init",
            """
            %%{init: {"flowchart": {"nodeSpacing": 80}, "theme": "forest"}}%%
            flowchart LR
                A --> B
            """);

        WriteFixture(
            BaseDir,
            "label-types",
            """
            flowchart TD
                N@{ label: "**bold**", labelType: markdown }
            """);

        WriteFixture(
            BaseDir,
            "label-html",
            """
            flowchart TD
                H@{ label: "<b>Hi</b>", labelType: html }
            """);

        WriteFixture(
            BaseDir,
            "class-def-style",
            """
            flowchart TD
                A --> B
                classDef highlight stroke:#f00
                class A highlight
                style B stroke:#00f
            """);

        WriteFixture(
            BaseDir,
            "interactions",
            """
            flowchart TD
                A --> B
                click A href "https://example.com"
            """);

        WriteFixture(
            BaseDir,
            "flowchart-accessibility",
            """
            accTitle: Flow title
            accDescr: Single line description
            flowchart TD
                A --> B
            """);

        WriteFixture(
            BaseDir,
            "accDescr-block",
            """
            accTitle: Block title
            accDescr {
              First line
              Second line
            }
            flowchart TD
                A --> B
            """);

        WriteFixture(
            BaseDir,
            "unsupported-diagnostics",
            """
            flowchart-elk TD
                A --> B
                click A myCallback
            """);

        WriteFixture(
            BaseDir,
            "arrow-variants",
            """
            flowchart TD
                A -.-> B
                C ==> D
            """);

        WriteFixture(
            BaseDir,
            "invisible-link",
            """
            flowchart TD
                A ~~~ B
            """);

        WriteFixture(
            BaseDir,
            "subgraph-direction",
            """
            flowchart TD
                subgraph Inner
                    direction LR
                    X --> Y
                end
            """);

        WriteFixture(
            BaseDir,
            "subgraph-nested",
            """
            flowchart TD
                subgraph Outer [Outer]
                    subgraph Inner [Inner]
                        A --> B
                    end
                end
            """);

        WriteFixture(
            BaseDir,
            "edge-metadata",
            """
            flowchart TD
                A e1@--> B
                e1@{ curve: basis }
            """);

        WriteFixture(
            BaseDir,
            "bidirectional-arrows",
            """
            flowchart LR
                A <--> B
                C o--o D
            """);

        WriteFixture(
            BaseDir,
            "edge-label-inline",
            """
            flowchart TD
                A -- authorize --> B
            """);

        WriteFixture(
            BaseDir,
            "linkStyle-interpolate",
            """
            flowchart TD
                A --> B
                linkStyle 0 interpolate basis
            """);

        WriteFixture(
            BaseDir,
            "link-minlen",
            """
            flowchart TD
                A ----> B
            """);

        WriteFixture(
            BaseDir,
            "edge-label-pipe",
            """
            flowchart TD
                A -->|Yes| B
            """);

        WriteFixture(
            BaseDir,
            "edge-animate",
            """
            flowchart TD
                A e1@--> B
                C e2@--> D
                E e3@--> F
                e1@{ animate: true }
                e2@{ animate: false }
                e3@{ animate: true }
                e3@{ animate: false }
            """);

        WriteFixture(
            BaseDir,
            "edge-curve",
            """
            flowchart TD
                A e1@--> B
                A uniqueName@--> C
                e1@{ curve: basis }
                uniqueName@{ curve: cardinal }
            """);

        WriteFixture(
            BaseDir,
            "config-front-matter",
            """
            ---
            config:
              flowchart:
                curve: linear
            ---
            flowchart LR
                A[A] --> B[B] --> C[C]
            """);

        WriteFixture(
            BaseDir,
            "config-front-matter-spacing",
            """
            ---
            config:
              flowchart:
                nodeSpacing: 60
                rankSpacing: 70
                diagramPadding: 12
            ---
            flowchart TB
                A --> B
            """);

        WriteFixture(
            BaseDir,
            "config-front-matter-theme",
            """
            ---
            config:
              theme: forest
              flowchart:
                curve: linear
            ---
            flowchart LR
                A --> B
            """);

        WriteFixture(
            BaseDir,
            "edge-metadata-f87",
            """
            flowchart LR
                A e1@==> B
                e1@{ animate: true }
            """);

        WriteFixture(
            BaseDir,
            "edge-class-animate",
            """
            flowchart LR
                A e1@--> B
                classDef animate stroke-dasharray:9,5,stroke-dashoffset:900,animation:dash 25s linear infinite;
                class e1 animate
            """);
    }

    static string GetFixtureDirectory()
    {
        return RecognitionFixturePaths.GetFlowchartFixtureDirectory();
    }

    static void WriteFixture(string BaseDir, string BaseName, string Source)
    {
        DiagramContract Contract = FlowchartRecognizer.Parse(Source);
        ContractSnapshot Snapshot = ContractSnapshot.FromDiagram(Contract);
        File.WriteAllText(Path.Combine(BaseDir, BaseName + ".verified.json"), Snapshot.ToJson());
        File.WriteAllText(Path.Combine(BaseDir, BaseName + ".mmd"), Source.TrimEnd() + Environment.NewLine);
    }
}
