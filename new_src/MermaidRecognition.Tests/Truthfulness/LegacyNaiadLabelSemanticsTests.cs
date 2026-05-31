namespace MermaidRecognition.Tests.Truthfulness;

/// <summary>
/// Documents that legacy Naiad still uses label-semantic routing (comparison baseline only).
/// </summary>
[TestFixture]
public sealed class LegacyNaiadLabelSemanticsTests
{
    [Test]
    public void Checklist_Legacy_Naiad_CoordinateAssignment_Uses_Yes_No_Labels()
    {
        string RepoRoot = FindRepositoryRoot();
        string CoordinateAssignmentPath = Path.Combine(
            RepoRoot,
            "extern",
            "Naiad",
            "src",
            "Naiad",
            "Layout",
            "CoordinateAssignment.cs");

        Assert.That(File.Exists(CoordinateAssignmentPath), Is.True, "Legacy Naiad source must exist for comparison baseline");

        string Source = File.ReadAllText(CoordinateAssignmentPath);
        Assert.That(Source, Does.Contain("StartsWith(\"Yes\""));
        Assert.That(Source, Does.Contain("StartsWith(\"No\""));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);

        while (Current != null)
        {
            string Candidate = Path.Combine(Current.FullName, "extern", "Naiad");
            if (Directory.Exists(Candidate))
            {
                return Current.FullName;
            }

            Current = Current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root containing extern/Naiad.");
    }
}
