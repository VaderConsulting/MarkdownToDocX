namespace MermaidRecognition.Tests.BacklogKinds;

/// <summary>
/// Regenerates L1+ line-pattern kind fixtures (gantt, mindmap, gitGraph).
/// </summary>
[TestFixture]
public sealed class BacklogKindsFixtureWriter
{
    [Test]
    public void Write_All_Gantt_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetGanttFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            gantt
                title Simple Gantt
                Task A :a1, 2024-01-01, 30d
                Task B :b1, 2024-01-15, 20d
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "section",
            """
            gantt
                title Sections
                section Phase 1
                Task A :a1, 2024-01-01, 10d
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "dateFormat",
            """
            gantt
                dateFormat YYYY-MM-DD
                title Date format
                Task A :a1, 2024-01-01, 5d
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "excludes",
            """
            gantt
                title Excludes
                excludes weekends
                Task A :a1, 2024-01-01, 5d
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "forest"}}%%
            gantt
                title Init leading
                Task A :a1, 2024-01-01, 3d
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "relative",
            """
            gantt
                dateFormat YYYY-MM-DD
                title Relative scheduling
                section Documentation
                task A :a, 2024-01-27, 2024-01-28
                task B :b, after a, 2024-01-30
                task C :c, 2024-01-20, until a
                task D :d, after c, until b
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "milestone",
            """
            gantt
                dateFormat YYYY-MM-DD
                title Milestones
                section Documentation
                Milestone task :milestone, 2014-01-01, 2014-01-04
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accessibility",
            """
            gantt
                accTitle: Gantt Diagram
                accDescr: Tasks for Q4
                dateFormat YYYY-MM-DD
                section Section
                A task :a1, 2014-01-01, 30d
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "inclusive-end",
            """
            gantt
                dateFormat YYYY-MM-DD
                inclusiveEndDates
                title Inclusive end
                Task A :a1, 2024-01-01, 5d
            """);
    }

    [Test]
    public void Write_All_Mindmap_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetMindmapFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            mindmap
              Root
                Branch A
                Branch B
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "default"}}%%
            mindmap
              Root
                Child
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "comment",
            """
            mindmap
            %% hierarchy note
              Root
                Leaf
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "hierarchy",
            """
            mindmap
              root
                child1
                  leaf1
                child2
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "shapes",
            """
            mindmap
              root((the root))
                child)cloud label(
                branch))bang label((
                leaf{{hex label}}
            """);
    }

    [Test]
    public void Write_All_GitGraph_Fixtures()
    {
        string BaseDir = RecognitionFixturePaths.GetGitGraphFixtureDirectory();

        KindFixtureFiles.Write(
            BaseDir,
            "simple",
            """
            gitGraph
                commit id: "1"
                branch main
                commit id: "2"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "init-leading",
            """
            %%{init: {"theme": "base"}}%%
            gitGraph
                commit id: "a"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "branch-history",
            """
            gitGraph
                commit id: "1"
                branch develop
                checkout develop
                commit id: "2"
                branch main
                checkout main
                commit id: "3"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "merge",
            """
            gitGraph
                commit
                branch newbranch
                checkout newbranch
                commit
                commit
                checkout main
                commit
                merge newbranch
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "commit-args",
            """
            gitGraph
                commit id: "c1" tag: "v1.0" type: HIGHLIGHT
                commit "message only"
            """);

        KindFixtureFiles.Write(
            BaseDir,
            "accessibility",
            """
            gitGraph
                accTitle: This is a title
                accDescr: This is a description
                commit
            """);
    }
}
