using System;
using System.IO;
using System.Windows.Forms;
using LiveEditor.Archi;

namespace LiveEditor;

/// <summary>
/// Application entry point for the Mermaid live editor.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the live editor window, or runs a headless smoke check with <c>--smoke</c>.
    /// </summary>
    /// <param name="Args">Optional <c>--smoke</c>.</param>
    /// <returns>Process exit code.</returns>
    [STAThread]
    private static int Main(string[] Args)
    {
        if (Args.Length > 0 && string.Equals(Args[0], "--smoke", StringComparison.OrdinalIgnoreCase))
        {
            return RunSmoke();
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }

    private static int RunSmoke()
    {
        const string Source =
            "enterprise" + "\n"
            + "  title Smoke model" + "\n"
            + "  application-component webShop \"Web Shop\"" + "\n"
            + "  application-service orderApi \"Order API\"" + "\n"
            + "  webShop -serving-> orderApi" + "\n";

        string TempRoot = Path.Combine(Path.GetTempPath(), "LiveEditorSmoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRoot);
        try
        {
            string ExchangePath = Path.Combine(TempRoot, "exchange.xml");
            string ArchimatePath = Path.Combine(TempRoot, "model.archimate");
            string CoArchiFolder = Path.Combine(TempRoot, "coarchi");
            Directory.CreateDirectory(CoArchiFolder);
            Directory.CreateDirectory(Path.Combine(CoArchiFolder, ".git"));

            if (!ArchiInterop.TryExportExchangeXml(Source, ExchangePath, out _, out string Error))
            {
                Console.Error.WriteLine("Exchange export failed: " + Error);
                return 1;
            }

            string ExchangeXml = File.ReadAllText(ExchangePath);
            if (!ArchiInterop.TryImportExchangeXml(ExchangeXml, out string FromExchange, out _, out Error))
            {
                Console.Error.WriteLine("Exchange import failed: " + Error);
                return 1;
            }

            if (FromExchange.IndexOf("webShop", StringComparison.Ordinal) < 0
                || FromExchange.IndexOf("orderApi", StringComparison.Ordinal) < 0
                || FromExchange.IndexOf("serving", StringComparison.Ordinal) < 0)
            {
                Console.Error.WriteLine("Exchange round-trip lost content.");
                Console.Error.WriteLine(FromExchange);
                return 1;
            }

            if (!ArchiInterop.TryExportArchimateFile(Source, ArchimatePath, out _, out Error))
            {
                Console.Error.WriteLine("ArchiMate export failed: " + Error);
                return 1;
            }

            if (!ArchiInterop.TryImportArchimateFile(ArchimatePath, out string FromArchimate, out _, out Error))
            {
                Console.Error.WriteLine("ArchiMate import failed: " + Error);
                return 1;
            }

            if (FromArchimate.IndexOf("webShop", StringComparison.Ordinal) < 0)
            {
                Console.Error.WriteLine("ArchiMate round-trip lost webShop.");
                return 1;
            }

            File.WriteAllText(Path.Combine(CoArchiFolder, "model.archimate"), ArchiNativeModelXml.Write(
                EnterpriseArchiMapper.TryFromMermaidSource(Source, out ArchiExchangeModel Model, out Error)
                    ? Model
                    : throw new InvalidOperationException(Error)));

            if (!ArchiInterop.TryImportCoArchiFolder(CoArchiFolder, out string FromCoArchi, out _, out Error))
            {
                Console.Error.WriteLine("coArchi open failed: " + Error);
                return 1;
            }

            if (FromCoArchi.IndexOf("orderApi", StringComparison.Ordinal) < 0)
            {
                Console.Error.WriteLine("coArchi open lost orderApi.");
                return 1;
            }

            if (!ArchiInterop.TrySaveCoArchiFolder(Source, CoArchiFolder, out _, out Error))
            {
                Console.Error.WriteLine("coArchi save failed: " + Error);
                return 1;
            }

            if (!ArchiInterop.TryExportArchimateFile("flowchart TD\n  A-->B", Path.Combine(TempRoot, "should-fail.archimate"), out _, out Error)
                && Error.IndexOf("enterprise", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Console.WriteLine("LiveEditor smoke OK");
                return 0;
            }

            Console.Error.WriteLine("Expected flowchart export to soft-fail.");
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(TempRoot, recursive: true);
            }
            catch
            {
            }
        }
    }
}
