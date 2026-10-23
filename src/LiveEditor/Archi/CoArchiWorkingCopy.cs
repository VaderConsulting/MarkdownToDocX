using System;
using System.IO;
using System.Text;

namespace LiveEditor.Archi;

/// <summary>
/// Opens and saves coArchi working-copy folders without touching Git metadata.
/// </summary>
internal static class CoArchiWorkingCopy
{
    public const string MODEL_FILE_NAME = "model.archimate";

    /// <summary>
    /// Loads <c>model.archimate</c> from a coArchi repository folder.
    /// </summary>
    /// <param name="FolderPath">Repository root folder.</param>
    /// <param name="Model">Mapped model when successful.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when import succeeded.</returns>
    public static bool TryOpenFolder(string FolderPath, out ArchiExchangeModel Model, out string ErrorMessage)
    {
        Model = new ArchiExchangeModel();
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            ErrorMessage = "coArchi folder not found.";
            return false;
        }

        string ModelPath = Path.Combine(FolderPath, MODEL_FILE_NAME);
        if (!File.Exists(ModelPath))
        {
            ErrorMessage = "Folder does not contain model.archimate (expected for coArchi).";
            return false;
        }

        return ArchiMateArchive.TryLoad(ModelPath, out Model, out ErrorMessage);
    }

    /// <summary>
    /// Writes <c>model.archimate</c> as non-archived XML into an existing coArchi folder.
    /// Does not create, modify, or delete <c>.git</c> or <c>images</c>.
    /// </summary>
    /// <param name="FolderPath">Repository root folder.</param>
    /// <param name="Model">Model to save.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when the file was written.</returns>
    public static bool TrySaveFolder(string FolderPath, ArchiExchangeModel Model, out string ErrorMessage)
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            ErrorMessage = "coArchi folder not found.";
            return false;
        }

        string ModelPath = Path.Combine(FolderPath, MODEL_FILE_NAME);
        if (!File.Exists(ModelPath))
        {
            ErrorMessage = "Refusing to create a new coArchi repository. Open an existing folder that already has model.archimate, then save.";
            return false;
        }

        string GitPath = Path.Combine(FolderPath, ".git");
        if (!Directory.Exists(GitPath) && !File.Exists(GitPath))
        {
            ErrorMessage = "Folder has model.archimate but no .git directory. Save only into a coArchi working copy, or use Export ArchiMate file for a standalone archive.";
            return false;
        }

        try
        {
            string Xml = ArchiNativeModelXml.Write(Model);
            File.WriteAllText(ModelPath, Xml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return true;
        }
        catch (Exception Ex)
        {
            ErrorMessage = "Could not save coArchi model.archimate: " + Ex.Message;
            return false;
        }
    }
}
