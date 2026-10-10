using System.IO;

namespace MermaidRecognition.Tests.Enterprise;

/// <summary>
/// Resolves <c>docs/enterprise/samples</c> from the test output or the repository root.
/// </summary>
internal static class EnterpriseSamplePaths
{
    internal static string SamplesDirectory
    {
        get
        {
            string? Located = Locate("docs", "enterprise", "samples");
            if (Located != null)
            {
                return Located;
            }

            string Copied = Path.Combine(AppContext.BaseDirectory, "EnterpriseSamples");
            if (Directory.Exists(Copied))
            {
                return Copied;
            }

            throw new DirectoryNotFoundException("Could not locate docs/enterprise/samples.");
        }
    }

    internal static string ReadSample(string FileName)
    {
        return File.ReadAllText(Path.Combine(SamplesDirectory, FileName));
    }

    private static string? Locate(params string[] RelativeParts)
    {
        DirectoryInfo? DirectoryCursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (DirectoryCursor != null)
        {
            string Candidate = Path.Combine(new[] { DirectoryCursor.FullName }.Concat(RelativeParts).ToArray());
            if (Directory.Exists(Candidate))
            {
                return Candidate;
            }

            DirectoryCursor = DirectoryCursor.Parent;
        }

        return null;
    }
}
