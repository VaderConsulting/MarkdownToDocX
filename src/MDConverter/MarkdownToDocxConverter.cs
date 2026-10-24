using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using MDConverter.Model;
using MDConverter.Options;
using MDConverter.Pipeline;

namespace MDConverter;

/// <summary>
/// Converts Markdown text to DOCX (bytes or stream). Primary use-case: Markdown in, DOCX out.
/// </summary>
/// <remarks>
/// Creates a converter with optional progress reporting.
/// </remarks>
public sealed class MarkdownToDocxConverter(MarkdownToDocxOptions Options, IConversionProgressReporter? ProgressReporter)
{
    internal static Action<string, double>? ConversionStageTimingObserver { get; set; }
    private static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly MarkdownToDocxOptions _Options = Options ?? throw new ArgumentNullException(nameof(Options));
    private readonly IConversionProgressReporter? _ProgressReporter = ProgressReporter;

    /// <summary>
    /// Creates a converter with the given options. Options are resolved (profile + overlay) at conversion time.
    /// </summary>
    public MarkdownToDocxConverter(MarkdownToDocxOptions Options)
        : this(Options, null)
    {
    }

    /// <summary>
    /// Converts Markdown to DOCX and returns the result as a byte array.
    /// When BasePath is null or empty, current directory is used for resolving relative image paths.
    /// </summary>
    public byte[] ConvertText(string Markdown, string BasePath)
    {
        using (MemoryStream OutputStream = new MemoryStream())
        {
            ConvertTextToStream(Markdown, BasePath, OutputStream);
            return OutputStream.ToArray();
        }
    }

    /// <summary>
    /// Converts Markdown to DOCX and writes the result to OutputStream.
    /// Resolves effective options (profile + overlay), parses Markdown into the internal model, then writes DOCX.
    /// </summary>
    public void ConvertTextToStream(string Markdown, string BasePath, Stream OutputStream)
    {
        if (OutputStream == null || !OutputStream.CanWrite)
        {
            throw new MarkdownToDocxException("Output stream must be non-null and writable.");
        }

        EffectiveOptions Effective = OptionsResolver.Resolve(_Options);

        if (Effective.Docx.UseBuiltinTemplate == false
            && string.IsNullOrWhiteSpace(Effective.Docx.TemplatePath))
        {
            throw new MarkdownToDocxException(
                "A template path is required when UseBuiltinTemplate is false.");
        }

        if (Effective.Docx.UseBuiltinTemplate == false
            && !System.IO.File.Exists(Effective.Docx.TemplatePath))
        {
            throw new MarkdownToDocxException(
                "Template file not found: " + Effective.Docx.TemplatePath);
        }

        string EffectiveBasePath = string.IsNullOrWhiteSpace(BasePath) ? Environment.CurrentDirectory : BasePath;

        _ProgressReporter?.Report(5, "Parsing Markdown");
        MarkdownToDocModelBuilder Builder = new MarkdownToDocModelBuilder(Effective.Markdown);
        ConversionStageTimingObserver?.Invoke("markdown-model-build", -1);
        Stopwatch ModelTimer = Stopwatch.StartNew();
        DocModel Model = Builder.Build(Markdown ?? string.Empty, EffectiveBasePath);
        ModelTimer.Stop();
        ConversionStageTimingObserver?.Invoke("markdown-model-build", ModelTimer.Elapsed.TotalMilliseconds);

        _ProgressReporter?.Report(15, "Writing DOCX");
        DocxWriter Writer = new DocxWriter(Effective, _ProgressReporter);
        ConversionStageTimingObserver?.Invoke("docx-write", -1);
        Stopwatch WriterTimer = Stopwatch.StartNew();
        Writer.Write(Model, OutputStream);
        WriterTimer.Stop();
        ConversionStageTimingObserver?.Invoke("docx-write", WriterTimer.Elapsed.TotalMilliseconds);
        _ProgressReporter?.Report(95, "Finalising document");
    }

    /// <summary>
    /// Convenience method for CLI/batch: reads Markdown from file (UTF-8), writes DOCX to file.
    /// BasePath is set to the directory of the Markdown file.
    /// </summary>
    public void ConvertFile(string MarkdownPath, string OutputDocxPath)
    {
        if (string.IsNullOrWhiteSpace(MarkdownPath))
        {
            throw new MarkdownToDocxException("Markdown path is required.");
        }
        if (string.IsNullOrWhiteSpace(OutputDocxPath))
        {
            throw new MarkdownToDocxException("Output path is required.");
        }

        _ProgressReporter?.Report(0, "Starting conversion");
        string FullOutputPath = Path.GetFullPath(OutputDocxPath);
        string DirectoryPath = Path.GetDirectoryName(FullOutputPath)!;
        Directory.CreateDirectory(DirectoryPath);
        string TemporaryPath = Path.Combine(DirectoryPath, ".md2docx-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            string Markdown = File.ReadAllText(MarkdownPath, UTF8_NO_BOM);
            string BasePath = Path.GetDirectoryName(MarkdownPath) ?? Environment.CurrentDirectory;
            using (FileStream OutputStream = new FileStream(TemporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                ConvertTextToStream(Markdown, BasePath, OutputStream);
            }

            // Replace only after conversion and package disposal have both succeeded.
            File.Move(TemporaryPath, FullOutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(TemporaryPath))
            {
                File.Delete(TemporaryPath);
            }
        }

        _ProgressReporter?.Report(100, "Complete");
    }

    /// <summary>
    /// Reads a Markdown file and returns DOCX bytes. Base path is the Markdown file directory.
    /// </summary>
    public byte[] ConvertMarkdownFile(string MarkdownPath)
    {
        if (string.IsNullOrWhiteSpace(MarkdownPath))
        {
            throw new MarkdownToDocxException("Markdown path is required.");
        }

        string Markdown = File.ReadAllText(MarkdownPath, UTF8_NO_BOM);
        string BasePath = Path.GetDirectoryName(MarkdownPath) ?? Environment.CurrentDirectory;
        return ConvertText(Markdown, BasePath);
    }

    /// <summary>
    /// Writes DOCX bytes to the output path. Throws <see cref="MarkdownToDocxException"/> when the file cannot be written.
    /// </summary>
    public static void WriteDocxBytes(string OutputDocxPath, byte[] DocxBytes)
    {
        if (string.IsNullOrWhiteSpace(OutputDocxPath))
        {
            throw new MarkdownToDocxException("Output path is required.");
        }
        if (DocxBytes == null)
        {
            throw new MarkdownToDocxException("DOCX bytes are required.");
        }

        try
        {
            File.WriteAllBytes(OutputDocxPath, DocxBytes);
        }
        catch (IOException Ex)
        {
            throw new MarkdownToDocxException(
                "Cannot write output file (it may be open in another process such as Word): " + OutputDocxPath,
                Ex);
        }
    }
}
