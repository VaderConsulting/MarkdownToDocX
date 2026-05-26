using System.Runtime.InteropServices;

namespace Converter;

internal static class Program
{
    [DllImport("kernel32.dll")]
    private static extern bool FreeConsole();

    /// <summary>
    /// Application entry point. No arguments opens the GUI; export flags or paired -in/-out run CLI and exit.
    /// </summary>
    [STAThread]
    private static int Main(string[] Args)
    {
        ApplicationConfiguration.Initialize();

        if (Args.Length == 0)
        {
            ReleaseConsoleForGui();
            Application.Run(new Main());
            return 0;
        }

        if (!CommandLineParser.TryParse(Args, out CommandLineRequest Request, out string ErrorMessage))
        {
            Console.Error.WriteLine(ErrorMessage);
            CommandLineHelp.WriteToConsole(Console.Error);
            return 1;
        }

        if (Request.ShowHelp)
        {
            CommandLineHelp.WriteToConsole();
            return 0;
        }

        if (Request.InputPathArguments.Count > 0)
        {
            if (!MarkdownInputResolver.TryResolve(Request.InputPathArguments, out List<string> MarkdownFiles, out string ResolveError))
            {
                Console.Error.WriteLine(ResolveError);
                CommandLineHelp.WriteToConsole(Console.Error);
                return 1;
            }

            Request.ResolvedMarkdownFiles = MarkdownFiles;
        }

        if (Request.HasExplicitDocxOutput
            && Request.ResolvedMarkdownFiles != null
            && Request.ResolvedMarkdownFiles.Count > 1)
        {
            Console.Error.WriteLine("A single -out or -docx path cannot be used with multiple input files.");
            Console.Error.WriteLine("Omit -out/-docx to write each <name>.docx beside its source file.");
            CommandLineHelp.WriteToConsole(Console.Error);
            return 1;
        }

        if (Request.HasExportFlags
            && Request.ResolvedMarkdownFiles != null
            && Request.ResolvedMarkdownFiles.Count > 1)
        {
            Console.Error.WriteLine("PNG/SVG export supports one input file only; use -index for multiple ```mermaid blocks in one file.");
            CommandLineHelp.WriteToConsole(Console.Error);
            return 1;
        }

        if (Request.RunAsCli)
        {
            return ConverterCliHost.Run(Request);
        }

        ReleaseConsoleForGui();
        string? GuiInputPath = null;
        if (Request.ResolvedMarkdownFiles != null && Request.ResolvedMarkdownFiles.Count == 1)
        {
            GuiInputPath = Request.ResolvedMarkdownFiles[0];
        }

        Application.Run(new Main(GuiInputPath, Request.OutputPath));
        return 0;
    }

    private static void ReleaseConsoleForGui()
    {
        if (OperatingSystem.IsWindows())
        {
            FreeConsole();
        }
    }

}
