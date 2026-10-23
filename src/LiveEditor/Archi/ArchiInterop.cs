using System;

namespace LiveEditor.Archi;

/// <summary>
/// High-level Archi / coArchi import and export helpers for the live editor UI.
/// </summary>
internal static class ArchiInterop
{
    /// <summary>
    /// Builds an exchange model from Mermaid enterprise source.
    /// </summary>
    /// <param name="Source">Editor source.</param>
    /// <param name="Model">Mapped model when successful.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when mapping succeeded.</returns>
    public static bool TryCreateModelFromSource(string? Source, out ArchiExchangeModel Model, out string ErrorMessage)
    {
        return EnterpriseArchiMapper.TryFromMermaidSource(Source, out Model, out ErrorMessage);
    }

    /// <summary>
    /// Imports Model Exchange XML text into Mermaid enterprise source.
    /// </summary>
    /// <param name="Xml">Exchange XML.</param>
    /// <param name="MermaidSource">Generated enterprise source.</param>
    /// <param name="WarningSummary">Optional warning text.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when import succeeded.</returns>
    public static bool TryImportExchangeXml(
        string Xml,
        out string MermaidSource,
        out string WarningSummary,
        out string ErrorMessage)
    {
        MermaidSource = string.Empty;
        WarningSummary = string.Empty;
        if (!ArchiMateExchangeXml.TryRead(Xml, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        MermaidSource = EnterpriseArchiMapper.ToMermaidSource(Model);
        WarningSummary = JoinWarnings(Model);
        return true;
    }

    /// <summary>
    /// Imports a standalone <c>.archimate</c> file into Mermaid enterprise source.
    /// </summary>
    /// <param name="Path">File path.</param>
    /// <param name="MermaidSource">Generated enterprise source.</param>
    /// <param name="WarningSummary">Optional warning text.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when import succeeded.</returns>
    public static bool TryImportArchimateFile(
        string Path,
        out string MermaidSource,
        out string WarningSummary,
        out string ErrorMessage)
    {
        MermaidSource = string.Empty;
        WarningSummary = string.Empty;
        if (!ArchiMateArchive.TryLoad(Path, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        MermaidSource = EnterpriseArchiMapper.ToMermaidSource(Model);
        WarningSummary = JoinWarnings(Model);
        return true;
    }

    /// <summary>
    /// Imports a coArchi working-copy folder into Mermaid enterprise source.
    /// </summary>
    /// <param name="FolderPath">Repository folder.</param>
    /// <param name="MermaidSource">Generated enterprise source.</param>
    /// <param name="WarningSummary">Optional warning text.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when import succeeded.</returns>
    public static bool TryImportCoArchiFolder(
        string FolderPath,
        out string MermaidSource,
        out string WarningSummary,
        out string ErrorMessage)
    {
        MermaidSource = string.Empty;
        WarningSummary = string.Empty;
        if (!CoArchiWorkingCopy.TryOpenFolder(FolderPath, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        MermaidSource = EnterpriseArchiMapper.ToMermaidSource(Model);
        WarningSummary = JoinWarnings(Model);
        return true;
    }

    /// <summary>
    /// Exports Mermaid source to Model Exchange XML on disk.
    /// </summary>
    /// <param name="Source">Editor source.</param>
    /// <param name="Path">Destination path.</param>
    /// <param name="WarningSummary">Optional warning text.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when export succeeded.</returns>
    public static bool TryExportExchangeXml(
        string? Source,
        string Path,
        out string WarningSummary,
        out string ErrorMessage)
    {
        WarningSummary = string.Empty;
        if (!TryCreateModelFromSource(Source, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        string Xml = ArchiMateExchangeXml.Write(Model);
        System.IO.File.WriteAllText(Path, Xml, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        WarningSummary = JoinWarnings(Model);
        return true;
    }

    /// <summary>
    /// Exports Mermaid source to a standalone <c>.archimate</c> ZIP.
    /// </summary>
    /// <param name="Source">Editor source.</param>
    /// <param name="Path">Destination path.</param>
    /// <param name="WarningSummary">Optional warning text.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when export succeeded.</returns>
    public static bool TryExportArchimateFile(
        string? Source,
        string Path,
        out string WarningSummary,
        out string ErrorMessage)
    {
        WarningSummary = string.Empty;
        if (!TryCreateModelFromSource(Source, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        ArchiMateArchive.Save(Path, Model);
        WarningSummary = JoinWarnings(Model);
        return true;
    }

    /// <summary>
    /// Saves Mermaid source into an existing coArchi working copy.
    /// </summary>
    /// <param name="Source">Editor source.</param>
    /// <param name="FolderPath">Repository folder.</param>
    /// <param name="WarningSummary">Optional warning text.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when save succeeded.</returns>
    public static bool TrySaveCoArchiFolder(
        string? Source,
        string FolderPath,
        out string WarningSummary,
        out string ErrorMessage)
    {
        WarningSummary = string.Empty;
        if (!TryCreateModelFromSource(Source, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        if (!CoArchiWorkingCopy.TrySaveFolder(FolderPath, Model, out ErrorMessage))
        {
            return false;
        }

        WarningSummary = JoinWarnings(Model);
        return true;
    }

    private static string JoinWarnings(ArchiExchangeModel Model)
    {
        if (Model.Warnings.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("; ", Model.Warnings);
    }
}
