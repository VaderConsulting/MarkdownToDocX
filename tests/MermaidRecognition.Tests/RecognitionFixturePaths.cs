namespace MermaidRecognition.Tests;

/// <summary>
/// Resolves fixture directories under the test project (never <c>bin/</c> output only).
/// </summary>
internal static class RecognitionFixturePaths
{
    /// <summary>
    /// Directory containing <c>MermaidRecognition.Tests.csproj</c>.
    /// </summary>
    public static string GetProjectDirectory()
    {
        string? AssemblyDirectory = Path.GetDirectoryName(typeof(RecognitionFixturePaths).Assembly.Location);
        if (AssemblyDirectory is null)
        {
            throw new InvalidOperationException("Cannot resolve test assembly directory.");
        }

        string? ProjectDirectory = Directory.GetParent(AssemblyDirectory)?.Parent?.Parent?.FullName;
        string ProjectFile = Path.Combine(ProjectDirectory ?? string.Empty, "MermaidRecognition.Tests.csproj");
        if (ProjectDirectory is null || !File.Exists(ProjectFile))
        {
            throw new InvalidOperationException(
                "Cannot resolve MermaidRecognition.Tests project directory from: " + AssemblyDirectory);
        }

        return ProjectDirectory;
    }

    public static string GetFlowchartFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "FlowchartRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetSequenceFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "SequenceRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetClassFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "ClassRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetStateFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "StateRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetErFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "ErRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetPieFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "PieRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetManifestsDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "Gates", "manifests");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetGanttFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "GanttRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetMindmapFixtureDirectory()
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), "MindmapRecognition");
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }

    public static string GetGitGraphFixtureDirectory()
    {
        return GetKindFixtureDirectory("GitGraphRecognition");
    }

    public static string GetTimelineFixtureDirectory()
    {
        return GetKindFixtureDirectory("TimelineRecognition");
    }

    public static string GetJourneyFixtureDirectory()
    {
        return GetKindFixtureDirectory("JourneyRecognition");
    }

    public static string GetQuadrantFixtureDirectory()
    {
        return GetKindFixtureDirectory("QuadrantRecognition");
    }

    public static string GetXychartFixtureDirectory()
    {
        return GetKindFixtureDirectory("XychartRecognition");
    }

    public static string GetRequirementFixtureDirectory()
    {
        return GetKindFixtureDirectory("RequirementRecognition");
    }

    public static string GetArchitectureFixtureDirectory()
    {
        return GetKindFixtureDirectory("ArchitectureRecognition");
    }

    public static string GetKanbanFixtureDirectory()
    {
        return GetKindFixtureDirectory("KanbanRecognition");
    }

    public static string GetSankeyFixtureDirectory()
    {
        return GetKindFixtureDirectory("SankeyRecognition");
    }

    public static string GetBlockFixtureDirectory()
    {
        return GetKindFixtureDirectory("BlockRecognition");
    }

    public static string GetPacketFixtureDirectory()
    {
        return GetKindFixtureDirectory("PacketRecognition");
    }

    public static string GetRadarFixtureDirectory()
    {
        return GetKindFixtureDirectory("RadarRecognition");
    }

    public static string GetTreemapFixtureDirectory()
    {
        return GetKindFixtureDirectory("TreemapRecognition");
    }

    public static string GetC4FixtureDirectory()
    {
        return GetKindFixtureDirectory("C4Recognition");
    }

    public static string GetKindFixtureDirectory(string FolderName)
    {
        string DirectoryPath = Path.Combine(GetProjectDirectory(), FolderName);
        Directory.CreateDirectory(DirectoryPath);
        return DirectoryPath;
    }
}
