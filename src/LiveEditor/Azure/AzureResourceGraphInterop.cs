using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using LiveEditor.Archi;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Azure;

/// <summary>
/// High-level Azure Resource Graph import for LiveEditor.
/// </summary>
internal static class AzureResourceGraphInterop
{
    /// <summary>
    /// Imports a Resource Graph CSV or JSON file into Mermaid enterprise source.
    /// </summary>
    public static bool TryImportFile(
        string FilePath,
        out string MermaidSource,
        out string WarningSummary,
        out string ErrorMessage)
    {
        return TryImportFile(FilePath, null, out MermaidSource, out WarningSummary, out ErrorMessage);
    }

    /// <summary>
    /// Imports a Resource Graph CSV or JSON file into Mermaid enterprise source.
    /// </summary>
    public static bool TryImportFile(
        string FilePath,
        AzureResourceGraphImportOptions? Options,
        out string MermaidSource,
        out string WarningSummary,
        out string ErrorMessage)
    {
        MermaidSource = string.Empty;
        WarningSummary = string.Empty;
        ErrorMessage = string.Empty;

        if (!LiveEditorFeatureGate.AE_AzureResourceGraphImport)
        {
            ErrorMessage = "Azure Resource Graph import is disabled.";
            return false;
        }

        if (!AzureResourceGraphReader.TryReadFile(FilePath, out List<AzureResourceGraphRecord> Records, out ErrorMessage))
        {
            return false;
        }

        AzureResourceGraphImportOptions EffectiveOptions = Options ?? AzureResourceGraphImportOptions.CreateEnterpriseDefaults();
        if (string.IsNullOrWhiteSpace(EffectiveOptions.ModelTitle) || string.Equals(EffectiveOptions.ModelTitle, "Azure resources", StringComparison.Ordinal))
        {
            string FileName = System.IO.Path.GetFileNameWithoutExtension(FilePath);
            if (!string.IsNullOrWhiteSpace(FileName))
            {
                EffectiveOptions.ModelTitle = "Azure: " + FileName;
            }
        }

        return TryImportRecords(Records, EffectiveOptions, out MermaidSource, out WarningSummary, out ErrorMessage);
    }

    /// <summary>Imports records retrieved directly from Azure or read from an export.</summary>
    public static bool TryImportRecords(
        IReadOnlyList<AzureResourceGraphRecord> Records,
        AzureResourceGraphImportOptions Options,
        out string MermaidSource,
        out string WarningSummary,
        out string ErrorMessage)
    {
        MermaidSource = string.Empty;
        WarningSummary = string.Empty;
        ErrorMessage = string.Empty;
        if (!LiveEditorFeatureGate.AE_AzureResourceGraphImport)
        {
            ErrorMessage = "Azure Resource Graph import is disabled.";
            return false;
        }

        if (!AzureResourceGraphEnterpriseMapper.TryMap(Records, Options, out ArchiExchangeModel Model, out ErrorMessage))
        {
            return false;
        }

        MermaidSource = EnterpriseArchiMapper.ToMermaidSource(Model);
        EnterpriseAnalysisResult Analysis = EnterpriseValidator.Analyze(MermaidSource);
        if (Analysis.HasErrors)
        {
            IReadOnlyList<EnterpriseDiagnostic> Errors = Analysis.Errors;
            string Message = Errors.Count > 0 ? Errors[0].Message : "validation failed";
            ErrorMessage = "Generated enterprise source did not parse: " + Message;
            MermaidSource = string.Empty;
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

        if (Model.Warnings.Count == 1)
        {
            return Model.Warnings[0];
        }

        return string.Join("; ", Model.Warnings)
            + " ("
            + Model.Warnings.Count.ToString(CultureInfo.InvariantCulture)
            + " notes)";
    }
}
