namespace Converter;

internal sealed class CommandLineRequest
{
    private readonly List<string> _InputPathArguments = new List<string>();

    public IReadOnlyList<string> InputPathArguments
    {
        get
        {
            return _InputPathArguments;
        }
    }

    public List<string>? ResolvedMarkdownFiles { get; set; }

    public string? InputPath { get; set; }

    public string? OutputPath { get; set; }

    public string? DocxPath { get; set; }

    public string? PngPath { get; set; }

    public string? SvgPath { get; set; }

    public int MermaidBlockIndex { get; set; } = 1;

    public bool ShowHelp { get; set; }

    public void AddInputPathArgument(string PathArgument)
    {
        _InputPathArguments.Add(PathArgument);
        InputPath = PathArgument;
    }

    public bool HasExportFlags
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PngPath) || !string.IsNullOrWhiteSpace(SvgPath))
            {
                return true;
            }

            return false;
        }
    }

    public bool HasExplicitDocxOutput
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(OutputPath) || !string.IsNullOrWhiteSpace(DocxPath))
            {
                return true;
            }

            return false;
        }
    }

    public bool RunAsCli
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(DocxPath)
                || !string.IsNullOrWhiteSpace(PngPath)
                || !string.IsNullOrWhiteSpace(SvgPath))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(InputPath) && !string.IsNullOrWhiteSpace(OutputPath))
            {
                return true;
            }

            if (RunBatchDocxConversion)
            {
                return true;
            }

            return false;
        }
    }

    public bool RunSilentDocxConversion
    {
        get
        {
            if (HasExportFlags)
            {
                return false;
            }

            if (!HasExplicitDocxOutput)
            {
                return false;
            }

            if (ResolvedMarkdownFiles == null || ResolvedMarkdownFiles.Count != 1)
            {
                return false;
            }

            if (LooksLikeBatchInput())
            {
                return false;
            }

            return true;
        }
    }

    public bool RunBatchDocxConversion
    {
        get
        {
            if (HasExportFlags)
            {
                return false;
            }

            if (HasExplicitDocxOutput)
            {
                return false;
            }

            if (ResolvedMarkdownFiles == null || ResolvedMarkdownFiles.Count == 0)
            {
                return false;
            }

            if (ResolvedMarkdownFiles.Count > 1)
            {
                return true;
            }

            if (_InputPathArguments.Count > 1)
            {
                return true;
            }

            if (_InputPathArguments.Count == 1 && MarkdownInputResolver.LooksLikeBatchInput(_InputPathArguments[0]))
            {
                return true;
            }

            return false;
        }
    }

    private bool LooksLikeBatchInput()
    {
        for (int Index = 0; Index < _InputPathArguments.Count; Index++)
        {
            if (MarkdownInputResolver.LooksLikeBatchInput(_InputPathArguments[Index]))
            {
                return true;
            }
        }

        return false;
    }
}
