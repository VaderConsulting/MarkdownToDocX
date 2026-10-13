using System;
using System.IO;
using System.Windows.Forms;
using MDConverter;
using MDConverter.Options;

namespace md2docx;

internal static class Program
{
    [STAThread]
    private static int Main(string[] Args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        ApplicationConfiguration.Initialize();

        if (Args.Length < 1)
        {
            WriteUsage();
            return 1;
        }

        string InputPath = Args[0];
        string OutputPath;
        int FlagStartIndex;

        if (Args.Length >= 2 && !Args[1].StartsWith('-'))
        {
            OutputPath = Args[1];
            FlagStartIndex = 2;
        }
        else
        {
            OutputPath = Path.ChangeExtension(Path.GetFullPath(InputPath), ".docx");
            FlagStartIndex = 1;
        }

        string Profile = "default";
        int? LockPromptTimeoutSeconds = null;
        bool ForceRecognitionFlowchartParser = false;
        bool ForceLegacyFlowchartParser = false;
        bool ForceLegacyFlowchartRenderer = false;
        MermaidRecognition.Config.DiagramLayoutMode? DefaultLayout = null;
        string? DefaultLegend = null;

        for (int I = FlagStartIndex; I < Args.Length; I++)
        {
            if (Args[I] == "--layout")
            {
                if (I + 1 >= Args.Length)
                {
                    Console.Error.WriteLine("--layout requires grid or open.");
                    return 1;
                }
                if (!MermaidRecognition.Config.DiagramLayoutSettings.TryParse(Args[++I], out var Layout))
                    Console.Error.WriteLine("Warning: Unknown layout value \"" + Args[I] + "\"; using grid.");
                DefaultLayout = Layout;
                continue;
            }
            if (Args[I] == "--legend")
            {
                if (I + 1 >= Args.Length)
                {
                    Console.Error.WriteLine("--legend requires off, on, a position, or table.");
                    return 1;
                }
                DefaultLegend = Args[++I];
                continue;
            }
            if (Args[I] == "--profile" && I + 1 < Args.Length)
            {
                Profile = Args[I + 1];
                I++;
                continue;
            }

            if (Args[I] == "--use-flowchart-recognition")
            {
                ForceRecognitionFlowchartParser = true;
                ForceLegacyFlowchartParser = false;
                continue;
            }

            if (Args[I] == "--use-legacy-flowchart-parser")
            {
                ForceLegacyFlowchartParser = true;
                ForceRecognitionFlowchartParser = false;
                continue;
            }

            if (Args[I] == "--use-legacy-flowchart-renderer")
            {
                ForceLegacyFlowchartRenderer = true;
                continue;
            }

            if (Args[I] == "--lock-timeout-seconds" && I + 1 < Args.Length)
            {
                if (!int.TryParse(Args[I + 1], out int ParsedTimeoutSeconds) || ParsedTimeoutSeconds <= 0)
                {
                    Console.Error.WriteLine("Invalid --lock-timeout-seconds value: " + Args[I + 1]);
                    return 1;
                }

                LockPromptTimeoutSeconds = ParsedTimeoutSeconds;
                I++;
            }
        }

        if (!File.Exists(InputPath))
        {
            Console.Error.WriteLine("Input file not found: " + InputPath);
            return 1;
        }

        MarkdownToDocxOptions Options = new MarkdownToDocxOptions
        {
            Profile = Profile,
            Mermaid = CreateMermaidOptions(ForceRecognitionFlowchartParser, ForceLegacyFlowchartParser, ForceLegacyFlowchartRenderer)
        };
        if (DefaultLayout.HasValue)
        {
            Options.Mermaid ??= new MermaidOptions();
            Options.Mermaid.DefaultLayout = DefaultLayout;
        }
        if (!string.IsNullOrWhiteSpace(DefaultLegend))
        {
            Options.Mermaid ??= new MermaidOptions();
            Options.Mermaid.DefaultLegend = DefaultLegend;
        }

        try
        {
            using ConsoleConversionProgressReporter ProgressReporter = new();
            MarkdownToDocxConverter Converter = new MarkdownToDocxConverter(Options, ProgressReporter);
            byte[] DocxBytes = Converter.ConvertMarkdownFile(InputPath);
            DocxOutputFileWriter.WriteAllBytes(OutputPath, DocxBytes, LockPromptTimeoutSeconds);
            Console.WriteLine("Converted: " + InputPath + " -> " + OutputPath);
            return 0;
        }
        catch (MarkdownToDocxException Ex)
        {
            Console.Error.WriteLine("Error: " + Ex.Message);
            return 1;
        }
    }

    private static void WriteUsage()
    {
        Console.Error.WriteLine("Usage: md2docx <input.md> [output.docx] [--profile <name>] [--layout grid|open] [--legend off|on|<position>|table] [--use-legacy-flowchart-parser] [--use-legacy-flowchart-renderer] [--use-flowchart-recognition] [--lock-timeout-seconds <n>]");
        Console.Error.WriteLine("  When output.docx is omitted, writes alongside the input file with the same base name and a .docx extension.");
        Console.Error.WriteLine("  Default behaviour: flowcharts use MermaidRecognition + native Sugiyama layout (legacy Naiad fallback).");
        Console.Error.WriteLine("  --use-legacy-flowchart-parser   Forces the legacy Naiad flowchart parser (for comparison/regression only).");
        Console.Error.WriteLine("  --use-legacy-flowchart-renderer Forces legacy Naiad SVG render (skips Sugiyama).");
        Console.Error.WriteLine("  --use-flowchart-recognition     Forces MermaidRecognition for flowcharts (kept for backwards compatibility).");
        Console.Error.WriteLine("  --lock-timeout-seconds  Optional. When omitted, a normal lock prompt is shown (no auto-cancel).");
        Console.Error.WriteLine("  --layout grid|open      Sets the run-wide placement default; diagram frontmatter wins.");
        Console.Error.WriteLine("  --legend <value>        Sets the run-wide legend default; diagram frontmatter wins.");
    }

    private static MermaidOptions? CreateMermaidOptions(
        bool ForceRecognitionFlowchartParser,
        bool ForceLegacyFlowchartParser,
        bool ForceLegacyFlowchartRenderer)
    {
        if (ForceRecognitionFlowchartParser && ForceLegacyFlowchartRenderer)
        {
            return new MermaidOptions
            {
                UseFlowchartRecognitionParser = true,
                UseNativeSugiyamaRenderer = false
            };
        }

        if (ForceRecognitionFlowchartParser)
        {
            return new MermaidOptions { UseFlowchartRecognitionParser = true };
        }

        if (ForceLegacyFlowchartParser)
        {
            return new MermaidOptions { UseFlowchartRecognitionParser = false };
        }

        if (ForceLegacyFlowchartRenderer)
        {
            return new MermaidOptions { UseNativeSugiyamaRenderer = false };
        }

        return null;
    }
}
