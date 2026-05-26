using System.Text;
using System.Threading.Tasks;
using MDConverter;
using MDConverter.Options;

namespace Converter;

internal static class ConverterCliHost
{
    private static readonly object _ConsoleSyncRoot = new object();

    public static int Run(CommandLineRequest Request)
    {
        ConsoleConversionProgressReporter ProgressReporter = new ConsoleConversionProgressReporter();
        try
        {
            if (Request.ResolvedMarkdownFiles == null || Request.ResolvedMarkdownFiles.Count == 0)
            {
                Console.Error.WriteLine("CLI mode requires -in <path>.");
                return 1;
            }

            if (Request.RunBatchDocxConversion)
            {
                return RunBatchDocxConversion(Request);
            }

            if (Request.RunSilentDocxConversion)
            {
                string InputMarkdownPath = Request.ResolvedMarkdownFiles[0];
                string OutputDocxPath = Request.OutputPath ?? Request.DocxPath!;
                return RunDocxConversion(InputMarkdownPath, OutputDocxPath, ProgressReporter);
            }

            string PrimaryInputPath = Request.ResolvedMarkdownFiles[0];

            if (!string.IsNullOrWhiteSpace(Request.DocxPath))
            {
                int Result = RunDocxConversion(PrimaryInputPath, Request.DocxPath, ProgressReporter);
                if (Result != 0)
                {
                    return Result;
                }
            }

            bool WroteImage = false;
            if (!string.IsNullOrWhiteSpace(Request.SvgPath) || !string.IsNullOrWhiteSpace(Request.PngPath))
            {
                ProgressReporter.Report(0, "Loading diagram source");
                string MermaidSource = MermaidSourceLoader.LoadMermaidSource(PrimaryInputPath, Request.MermaidBlockIndex);
                if (!string.IsNullOrWhiteSpace(Request.SvgPath))
                {
                    string Svg = MermaidDiagramExport.RenderSvg(MermaidSource, ProgressReporter);
                    WriteTextFile(Request.SvgPath, Svg);
                    Console.WriteLine("Wrote: " + Request.SvgPath);
                    WroteImage = true;
                }

                if (!string.IsNullOrWhiteSpace(Request.PngPath))
                {
                    byte[]? PngBytes = MermaidDiagramExport.TryRenderPng(MermaidSource, ProgressReporter);
                    if (PngBytes == null || PngBytes.Length == 0)
                    {
                        Console.Error.WriteLine("PNG rasterisation failed.");
                        return 1;
                    }

                    WriteBytesFile(Request.PngPath, PngBytes);
                    Console.WriteLine("Wrote: " + Request.PngPath);
                    WroteImage = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(Request.DocxPath) || WroteImage)
            {
                return 0;
            }

            Console.Error.WriteLine("CLI mode requires -docx, -png, and/or -svg, or both -in and -out for DOCX conversion.");
            return 1;
        }
        catch (Exception Ex)
        {
            Console.Error.WriteLine("Error: " + Ex.Message);
            return 1;
        }
    }

    private static int RunBatchDocxConversion(CommandLineRequest Request)
    {
        List<string> MarkdownFiles = Request.ResolvedMarkdownFiles!;
        int TotalCount = MarkdownFiles.Count;
        int MaxParallelism = GetBatchMaxDegreeOfParallelism();
        int FailureCount = 0;

        WriteStandardOutputLine(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Batch: {TotalCount} file(s), up to {MaxParallelism} parallel worker(s)."));

        ParallelOptions ParallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = MaxParallelism
        };

        Parallel.For(0, TotalCount, ParallelOptions, FileIndex =>
        {
            string InputMarkdownPath = MarkdownFiles[FileIndex];
            string OutputDocxPath = MarkdownInputResolver.ToSiblingDocxPath(InputMarkdownPath);
            WriteStandardOutputLine(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"[{FileIndex + 1}/{TotalCount}] {Path.GetFileName(InputMarkdownPath)}"));

            int Result = RunDocxConversion(InputMarkdownPath, OutputDocxPath, null);
            if (Result != 0)
            {
                System.Threading.Interlocked.Increment(ref FailureCount);
            }
        });

        if (FailureCount > 0)
        {
            WriteStandardErrorLine(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"Batch finished with {FailureCount} failure(s) out of {TotalCount}."));
            return 1;
        }

        WriteStandardOutputLine(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Batch converted {TotalCount} file(s)."));
        return 0;
    }

    private static int GetBatchMaxDegreeOfParallelism()
    {
        int ProcessorCount = Environment.ProcessorCount;
        int MaxWorkers = ProcessorCount - 1;
        if (MaxWorkers < 1)
        {
            MaxWorkers = 1;
        }

        return MaxWorkers;
    }

    private static int RunDocxConversion(
        string InputPath,
        string OutputDocxPath,
        IConversionProgressReporter? ProgressReporter)
    {
        if (!File.Exists(InputPath))
        {
            WriteStandardErrorLine("Input file not found: " + InputPath);
            return 1;
        }

        try
        {
            MarkdownToDocxOptions Options = new MarkdownToDocxOptions();
            MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options, ProgressReporter);
            Converter.ConvertFile(InputPath, OutputDocxPath);
            WriteStandardOutputLine("Converted: " + InputPath + " -> " + OutputDocxPath);
            return 0;
        }
        catch (MarkdownToDocxException Ex)
        {
            WriteStandardErrorLine("Error: " + Ex.Message);
            return 1;
        }
    }

    private static void WriteStandardOutputLine(string Line)
    {
        lock (_ConsoleSyncRoot)
        {
            Console.Out.WriteLine(Line);
        }
    }

    private static void WriteStandardErrorLine(string Line)
    {
        lock (_ConsoleSyncRoot)
        {
            Console.Error.WriteLine(Line);
        }
    }

    private static void WriteTextFile(string PathValue, string Content)
    {
        string? DirectoryPath = Path.GetDirectoryName(PathValue);
        if (!string.IsNullOrWhiteSpace(DirectoryPath))
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        File.WriteAllText(PathValue, Content, new UTF8Encoding(false));
    }

    private static void WriteBytesFile(string PathValue, byte[] Bytes)
    {
        string? DirectoryPath = Path.GetDirectoryName(PathValue);
        if (!string.IsNullOrWhiteSpace(DirectoryPath))
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        File.WriteAllBytes(PathValue, Bytes);
    }
}
