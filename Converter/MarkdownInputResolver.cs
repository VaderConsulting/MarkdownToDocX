namespace Converter;

internal static class MarkdownInputResolver
{
    private static readonly string[] MARKDOWN_EXTENSIONS = { ".md", ".markdown", ".txt" };

    public static bool TryResolve(
        IReadOnlyList<string> InputArguments,
        out List<string> MarkdownFiles,
        out string ErrorMessage)
    {
        MarkdownFiles = [];
        ErrorMessage = string.Empty;

        if (InputArguments == null || InputArguments.Count == 0)
        {
            ErrorMessage = "No input paths were specified.";
            return false;
        }

        HashSet<string> SeenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int Index = 0; Index < InputArguments.Count; Index++)
        {
            string Argument = InputArguments[Index];
            if (string.IsNullOrWhiteSpace(Argument))
            {
                continue;
            }

            string FullPath = Path.GetFullPath(Argument);
            if (Directory.Exists(FullPath))
            {
                AddFilesFromDirectory(FullPath, SearchOption.TopDirectoryOnly, SeenPaths, MarkdownFiles);
                continue;
            }

            if (ContainsWildcard(Argument))
            {
                if (!TryAddGlobMatches(Argument, SeenPaths, MarkdownFiles, out ErrorMessage))
                {
                    return false;
                }

                continue;
            }

            if (File.Exists(FullPath))
            {
                if (!IsMarkdownOrTextFile(FullPath))
                {
                    ErrorMessage = "Input file is not a Markdown or text file: " + FullPath;
                    return false;
                }

                AddUniqueFile(FullPath, SeenPaths, MarkdownFiles);
                continue;
            }

            if (TryAddGlobMatches(Argument, SeenPaths, MarkdownFiles, out string GlobError))
            {
                continue;
            }

            if (TryAddGlobMatches(FullPath, SeenPaths, MarkdownFiles, out GlobError))
            {
                continue;
            }

            ErrorMessage = "Input path not found: " + Argument;
            return false;
        }

        if (MarkdownFiles.Count == 0)
        {
            ErrorMessage = "No Markdown or text files matched the input path(s).";
            return false;
        }

        MarkdownFiles.Sort(StringComparer.OrdinalIgnoreCase);
        return true;
    }

    public static bool LooksLikeBatchInput(string Argument)
    {
        if (string.IsNullOrWhiteSpace(Argument))
        {
            return false;
        }

        if (ContainsWildcard(Argument))
        {
            return true;
        }

        string FullPath = Path.GetFullPath(Argument);
        if (Directory.Exists(FullPath))
        {
            return true;
        }

        return false;
    }

    public static string ToSiblingDocxPath(string MarkdownPath)
    {
        string? DirectoryPath = Path.GetDirectoryName(MarkdownPath);
        string BaseName = Path.GetFileNameWithoutExtension(MarkdownPath);
        if (string.IsNullOrWhiteSpace(DirectoryPath))
        {
            return BaseName + ".docx";
        }

        return Path.Combine(DirectoryPath, BaseName + ".docx");
    }

    private static void AddFilesFromDirectory(
        string DirectoryPath,
        SearchOption SearchOption,
        HashSet<string> SeenPaths,
        List<string> MarkdownFiles)
    {
        for (int ExtensionIndex = 0; ExtensionIndex < MARKDOWN_EXTENSIONS.Length; ExtensionIndex++)
        {
            string Pattern = "*" + MARKDOWN_EXTENSIONS[ExtensionIndex];
            string[] Files = Directory.GetFiles(DirectoryPath, Pattern, SearchOption);
            for (int FileIndex = 0; FileIndex < Files.Length; FileIndex++)
            {
                AddUniqueFile(Files[FileIndex], SeenPaths, MarkdownFiles);
            }
        }
    }

    private static bool TryAddGlobMatches(
        string Pattern,
        HashSet<string> SeenPaths,
        List<string> MarkdownFiles,
        out string ErrorMessage)
    {
        ErrorMessage = string.Empty;
        string NormalisedPattern = Pattern.Replace('/', Path.DirectorySeparatorChar);

        string DirectoryPath;
        string SearchPattern;
        SearchOption SearchOption;
        if (NormalisedPattern.Contains("**", StringComparison.Ordinal))
        {
            if (!TryParseRecursiveSearchPattern(NormalisedPattern, out DirectoryPath, out SearchPattern, out ErrorMessage))
            {
                return false;
            }

            SearchOption = SearchOption.AllDirectories;
        }
        else
        {
            string FullPattern = Path.GetFullPath(NormalisedPattern);
            DirectoryPath = Path.GetDirectoryName(FullPattern) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(DirectoryPath))
            {
                DirectoryPath = Environment.CurrentDirectory;
            }

            SearchPattern = Path.GetFileName(FullPattern);
            if (string.IsNullOrWhiteSpace(SearchPattern))
            {
                SearchPattern = "*.*";
            }

            SearchOption = SearchOption.TopDirectoryOnly;
        }

        if (!Directory.Exists(DirectoryPath))
        {
            ErrorMessage = "Directory not found for pattern: " + Pattern;
            return false;
        }

        string[] MatchedFiles = Directory.GetFiles(DirectoryPath, SearchPattern, SearchOption);
        bool AddedAny = false;
        for (int Index = 0; Index < MatchedFiles.Length; Index++)
        {
            string FilePath = MatchedFiles[Index];
            if (!IsMarkdownOrTextFile(FilePath))
            {
                continue;
            }

            AddUniqueFile(FilePath, SeenPaths, MarkdownFiles);
            AddedAny = true;
        }

        if (!AddedAny)
        {
            ErrorMessage = "No Markdown or text files matched pattern: " + Pattern;
            return false;
        }

        return true;
    }

    private static void AddUniqueFile(string FilePath, HashSet<string> SeenPaths, List<string> MarkdownFiles)
    {
        string FullPath = Path.GetFullPath(FilePath);
        if (SeenPaths.Add(FullPath))
        {
            MarkdownFiles.Add(FullPath);
        }
    }

    private static bool IsMarkdownOrTextFile(string FilePath)
    {
        string Extension = Path.GetExtension(FilePath);
        for (int Index = 0; Index < MARKDOWN_EXTENSIONS.Length; Index++)
        {
            if (string.Equals(Extension, MARKDOWN_EXTENSIONS[Index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseRecursiveSearchPattern(
        string Pattern,
        out string DirectoryPath,
        out string SearchPattern,
        out string ErrorMessage)
    {
        DirectoryPath = string.Empty;
        SearchPattern = string.Empty;
        ErrorMessage = string.Empty;

        int RecursiveIndex = Pattern.IndexOf("**", StringComparison.Ordinal);
        string DirectoryPart = Pattern.Substring(0, RecursiveIndex);
        DirectoryPart = DirectoryPart.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        string Remainder = Pattern.Substring(RecursiveIndex + 2);
        Remainder = Remainder.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(DirectoryPart))
        {
            DirectoryPath = Environment.CurrentDirectory;
        }
        else
        {
            DirectoryPath = Path.GetFullPath(DirectoryPart);
        }

        if (string.IsNullOrWhiteSpace(Remainder))
        {
            SearchPattern = "*.*";
            return true;
        }

        if (Remainder.Contains(Path.DirectorySeparatorChar) || Remainder.Contains(Path.AltDirectorySeparatorChar))
        {
            ErrorMessage = "Recursive patterns support a file-name wildcard only after ** (e.g. docs\\**\\files*.md).";
            return false;
        }

        SearchPattern = Remainder;
        return true;
    }

    private static bool ContainsWildcard(string Text)
    {
        return Text.IndexOf('*') >= 0 || Text.IndexOf('?') >= 0;
    }
}
