using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace LiveEditor.Archi;

/// <summary>
/// Reads and writes standalone Archi <c>.archimate</c> ZIP archives.
/// </summary>
internal static class ArchiMateArchive
{
    private const string MODEL_ENTRY_NAME = "model.xml";

    /// <summary>
    /// Loads an exchange model from a <c>.archimate</c> file (ZIP or raw XML).
    /// </summary>
    /// <param name="Path">File path.</param>
    /// <param name="Model">Mapped model when successful.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when import succeeded.</returns>
    public static bool TryLoad(string Path, out ArchiExchangeModel Model, out string ErrorMessage)
    {
        Model = new ArchiExchangeModel();
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Path) || !File.Exists(Path))
        {
            ErrorMessage = "ArchiMate file not found.";
            return false;
        }

        if (!TryReadModelXml(Path, out string Xml, out ErrorMessage))
        {
            return false;
        }

        return ArchiNativeModelXml.TryRead(Xml, out Model, out ErrorMessage);
    }

    /// <summary>
    /// Saves an exchange model as a standalone <c>.archimate</c> ZIP.
    /// </summary>
    /// <param name="Path">Destination path.</param>
    /// <param name="Model">Model to write.</param>
    public static void Save(string Path, ArchiExchangeModel Model)
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            throw new ArgumentException("Output path is required.", nameof(Path));
        }

        string Xml = ArchiNativeModelXml.Write(Model);
        string? DirectoryPath = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(DirectoryPath))
        {
            Directory.CreateDirectory(DirectoryPath);
        }

        if (File.Exists(Path))
        {
            File.Delete(Path);
        }

        using ZipArchive Archive = ZipFile.Open(Path, ZipArchiveMode.Create);
        ZipArchiveEntry Entry = Archive.CreateEntry(MODEL_ENTRY_NAME, CompressionLevel.Optimal);
        using Stream EntryStream = Entry.Open();
        using StreamWriter Writer = new StreamWriter(EntryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Writer.Write(Xml);
    }

    private static bool TryReadModelXml(string Path, out string Xml, out string ErrorMessage)
    {
        Xml = string.Empty;
        ErrorMessage = string.Empty;

        byte[] Bytes = File.ReadAllBytes(Path);
        if (Bytes.Length >= 2 && Bytes[0] == (byte)'P' && Bytes[1] == (byte)'K')
        {
            try
            {
                using ZipArchive Archive = ZipFile.OpenRead(Path);
                ZipArchiveEntry? Entry = Archive.GetEntry(MODEL_ENTRY_NAME);
                if (Entry == null)
                {
                    foreach (ZipArchiveEntry Candidate in Archive.Entries)
                    {
                        if (Candidate.FullName.EndsWith("model.xml", StringComparison.OrdinalIgnoreCase)
                            || Candidate.FullName.EndsWith(".archimate", StringComparison.OrdinalIgnoreCase))
                        {
                            Entry = Candidate;
                            break;
                        }
                    }
                }

                if (Entry == null)
                {
                    ErrorMessage = "The .archimate archive does not contain model.xml.";
                    return false;
                }

                using Stream Stream = Entry.Open();
                using StreamReader Reader = new StreamReader(Stream, Encoding.UTF8);
                Xml = Reader.ReadToEnd();
                return true;
            }
            catch (Exception Ex)
            {
                ErrorMessage = "Could not read .archimate archive: " + Ex.Message;
                return false;
            }
        }

        Xml = Encoding.UTF8.GetString(Bytes);
        if (Xml.IndexOf("<", StringComparison.Ordinal) < 0)
        {
            ErrorMessage = "File is neither a ZIP .archimate archive nor Archi model XML.";
            return false;
        }

        return true;
    }
}
