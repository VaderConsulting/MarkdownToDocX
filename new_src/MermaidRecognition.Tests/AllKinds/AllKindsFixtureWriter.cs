namespace MermaidRecognition.Tests.AllKinds;

/// <summary>
/// Regenerates verified JSON for all remaining §3.18.2 line-pattern kinds.
/// </summary>
[TestFixture]
public sealed class AllKindsFixtureWriter
{
    [Test]
    [Explicit("Regenerates baseline fixtures; select this test explicitly to update them.")]
    public void Write_All_Remaining_Kind_Fixtures()
    {
        WriteTimeline();
        WriteJourney();
        WriteQuadrant();
        WriteXychart();
        WriteRequirement();
        WriteArchitecture();
        WriteKanban();
        WriteSankey();
        WriteBlock();
        WritePacket();
        WriteRadar();
        WriteTreemap();
        WriteC4();
    }

    static void WriteTimeline()
    {
        string BaseDir = RecognitionFixturePaths.GetTimelineFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            timeline
                title History
                2020 : Event One
                2021 : Event Two
            """);
    }

    static void WriteJourney()
    {
        string BaseDir = RecognitionFixturePaths.GetJourneyFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            journey
                title My Working Day
                section Morning
                    Make coffee: 5: Me
                    Check emails: 3: Me
            """);
    }

    static void WriteQuadrant()
    {
        string BaseDir = RecognitionFixturePaths.GetQuadrantFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            quadrantChart
                title Campaign Analysis
                x-axis Low Reach --> High Reach
                y-axis Low Engagement --> High Engagement
                Campaign A: [0.3, 0.6]
            """);
    }

    static void WriteXychart()
    {
        string BaseDir = RecognitionFixturePaths.GetXychartFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            xychart-beta
                title "Monthly Sales"
                x-axis [Jan, Feb, Mar]
                y-axis "Revenue" 0 --> 100
                bar [50, 60, 75]
            """);
    }

    static void WriteRequirement()
    {
        string BaseDir = RecognitionFixturePaths.GetRequirementFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            requirementDiagram

            requirement test_req {
                id: 1
                text: The system shall do something
                risk: high
                verifymethod: test
            }
            """);
    }

    static void WriteArchitecture()
    {
        string BaseDir = RecognitionFixturePaths.GetArchitectureFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            architecture-beta
            service db(database)[Database]
            service api(server)[API]
            """);
    }

    static void WriteKanban()
    {
        string BaseDir = RecognitionFixturePaths.GetKanbanFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            kanban
            todo[Todo]
                task1[First Task]
            done[Done]
                task2[Completed Task]
            """);
    }

    static void WriteSankey()
    {
        string BaseDir = RecognitionFixturePaths.GetSankeyFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            sankey-beta
            A,B,10
            A,C,20
            """);
    }

    static void WriteBlock()
    {
        string BaseDir = RecognitionFixturePaths.GetBlockFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            block-beta
                columns 3
                a["Block A"] b["Block B"] c["Block C"]
            """);
    }

    static void WritePacket()
    {
        string BaseDir = RecognitionFixturePaths.GetPacketFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            packet-beta
            0-15: "Source Port"
            16-31: "Destination Port"
            """);
    }

    static void WriteRadar()
    {
        string BaseDir = RecognitionFixturePaths.GetRadarFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            radar-beta
            axis A, B, C
            curve data1["Series1"]{20, 40, 60}
            """);
    }

    static void WriteTreemap()
    {
        string BaseDir = RecognitionFixturePaths.GetTreemapFixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            treemap-beta
            "Section A"
                "Item 1": 30
                "Item 2": 20
            """);
    }

    static void WriteC4()
    {
        string BaseDir = RecognitionFixturePaths.GetC4FixtureDirectory();
        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            C4Context
                title System Context
                Person(user, "User", "A user")
                System(system, "System", "Main system")
                Rel(user, system, "Uses")
            """);
    }
}
