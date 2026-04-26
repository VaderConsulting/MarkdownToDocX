using System;
using System.IO;
using System.Text;
using MDConverter.Model;
using MDConverter.Options;
using MDConverter.Pipeline;

namespace MDConverter;

/// <summary>
/// Converts Markdown text to DOCX (bytes or stream). Primary use-case: Markdown in, DOCX out.
/// </summary>
public sealed class MarkdownToDocxConverter
{
    private static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly MarkdownToDocxOptions _Options;

    /// <summary>
    /// Creates a converter with the given options. Options are resolved (profile + overlay) at conversion time.
    /// </summary>
    public MarkdownToDocxConverter(MarkdownToDocxOptions Options) => this._Options = Options ?? throw new ArgumentNullException(nameof(Options));

    /// <summary>
    /// Converts Markdown to DOCX and returns the result as a byte array.
    /// When BasePath is null or empty, current directory is used for resolving relative image paths.
    /// </summary>
    public byte[] ConvertText(string Markdown, string BasePath)
    {
        using (var OutputStream = new MemoryStream())
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

        var Effective = OptionsResolver.Resolve(_Options);

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

        var EffectiveBasePath = string.IsNullOrWhiteSpace(BasePath) ? Environment.CurrentDirectory : BasePath;

        var Builder = new MarkdownToDocModelBuilder(Effective.Markdown);
        var Model = Builder.Build(Markdown ?? string.Empty, EffectiveBasePath);

        var Writer = new DocxWriter(Effective);
        Writer.Write(Model, OutputStream);
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

        var Markdown = File.ReadAllText(MarkdownPath, UTF8_NO_BOM);
        var BasePath = Path.GetDirectoryName(MarkdownPath) ?? Environment.CurrentDirectory;

        var DocxBytes = ConvertText(Markdown, BasePath);
        File.WriteAllBytes(OutputDocxPath, DocxBytes);
    }
}
