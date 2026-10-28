using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace LiveEditor.Azure;

/// <summary>
/// Reads Azure Resource Graph Explorer CSV and JSON exports.
/// </summary>
internal static class AzureResourceGraphReader
{
    /// <summary>
    /// Reads records from a file path based on extension.
    /// </summary>
    /// <param name="Path">CSV or JSON file.</param>
    /// <param name="Records">Parsed rows when successful.</param>
    /// <param name="ErrorMessage">Failure reason when unsuccessful.</param>
    /// <returns>True when at least one record was read.</returns>
    public static bool TryReadFile(string FilePath, out List<AzureResourceGraphRecord> Records, out string ErrorMessage)
    {
        Records = [];
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(FilePath) || !File.Exists(FilePath))
        {
            ErrorMessage = "File was not found.";
            return false;
        }

        string Extension = System.IO.Path.GetExtension(FilePath).ToLowerInvariant();
        try
        {
            string Text = File.ReadAllText(FilePath);
            if (Extension == ".json")
            {
                return TryReadJson(Text, out Records, out ErrorMessage);
            }

            return TryReadCsv(Text, out Records, out ErrorMessage);
        }
        catch (Exception Ex)
        {
            ErrorMessage = Ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Parses a CSV export (header row required).
    /// </summary>
    public static bool TryReadCsv(string CsvText, out List<AzureResourceGraphRecord> Records, out string ErrorMessage)
    {
        Records = [];
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(CsvText))
        {
            ErrorMessage = "CSV is empty.";
            return false;
        }

        using StringReader Reader = new StringReader(CsvText);
        using TextFieldParser Parser = new TextFieldParser(Reader)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        Parser.SetDelimiters(",");

        if (Parser.EndOfData)
        {
            ErrorMessage = "CSV has no header row.";
            return false;
        }

        string[] Header = Parser.ReadFields() ?? Array.Empty<string>();
        Dictionary<string, int> IndexByName = BuildHeaderIndex(Header);
        if (!IndexByName.ContainsKey("id") || !IndexByName.ContainsKey("name") || !IndexByName.ContainsKey("type"))
        {
            ErrorMessage = "CSV header must include at least id, name, and type columns (Resource Graph default export).";
            return false;
        }

        while (!Parser.EndOfData)
        {
            string[] Fields = Parser.ReadFields() ?? Array.Empty<string>();
            AzureResourceGraphRecord? Record = ReadCsvRow(Fields, IndexByName);
            if (Record != null)
            {
                Records.Add(Record);
            }
        }

        if (Records.Count == 0)
        {
            ErrorMessage = "No resource rows were found in the CSV.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Parses a JSON array export.
    /// </summary>
    public static bool TryReadJson(string JsonText, out List<AzureResourceGraphRecord> Records, out string ErrorMessage)
    {
        Records = [];
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(JsonText))
        {
            ErrorMessage = "JSON is empty.";
            return false;
        }

        using JsonDocument Document = JsonDocument.Parse(JsonText);
        if (Document.RootElement.ValueKind != JsonValueKind.Array)
        {
            ErrorMessage = "JSON must be an array of resources (Resource Graph download format).";
            return false;
        }

        foreach (JsonElement Element in Document.RootElement.EnumerateArray())
        {
            AzureResourceGraphRecord Record = ReadJsonObject(Element);
            if (!string.IsNullOrWhiteSpace(Record.Id))
            {
                Records.Add(Record);
            }
        }

        if (Records.Count == 0)
        {
            ErrorMessage = "No resources were found in the JSON array.";
            return false;
        }

        return true;
    }

    private static Dictionary<string, int> BuildHeaderIndex(string[] Header)
    {
        Dictionary<string, int> IndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int Index = 0; Index < Header.Length; Index++)
        {
            string Name = Header[Index].Trim();
            if (Name.Length > 0 && !IndexByName.ContainsKey(Name))
            {
                IndexByName.Add(Name, Index);
            }
        }

        return IndexByName;
    }

    private static AzureResourceGraphRecord? ReadCsvRow(string[] Fields, Dictionary<string, int> IndexByName)
    {
        string Id = ReadField(Fields, IndexByName, "id");
        if (string.IsNullOrWhiteSpace(Id))
        {
            return null;
        }

        AzureResourceGraphRecord Record = new AzureResourceGraphRecord
        {
            SubscriptionId = ReadField(Fields, IndexByName, "subscriptionId"),
            ResourceGroup = ReadField(Fields, IndexByName, "resourceGroup"),
            Name = ReadField(Fields, IndexByName, "name"),
            Type = ReadField(Fields, IndexByName, "type"),
            Location = ReadField(Fields, IndexByName, "location"),
            Kind = ReadField(Fields, IndexByName, "kind"),
            Sku = ReadField(Fields, IndexByName, "sku"),
            TagsJson = ReadField(Fields, IndexByName, "tags"),
            Id = Id
        };
        return Record;
    }

    private static string ReadField(string[] Fields, Dictionary<string, int> IndexByName, string ColumnName)
    {
        if (!IndexByName.TryGetValue(ColumnName, out int Index))
        {
            return string.Empty;
        }

        if (Index < 0 || Index >= Fields.Length)
        {
            return string.Empty;
        }

        string Value = Fields[Index];
        if (string.Equals(Value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return Value.Trim();
    }

    internal static AzureResourceGraphRecord ReadJsonObject(JsonElement Element)
    {
        AzureResourceGraphRecord Record = new AzureResourceGraphRecord
        {
            SubscriptionId = ReadJsonString(Element, "subscriptionId"),
            ResourceGroup = ReadJsonString(Element, "resourceGroup"),
            Name = ReadJsonString(Element, "name"),
            Type = ReadJsonString(Element, "type"),
            Location = ReadJsonString(Element, "location"),
            Kind = ReadJsonString(Element, "kind"),
            Sku = ReadJsonSku(Element),
            TagsJson = ReadJsonTags(Element),
            Id = ReadJsonString(Element, "id")
        };
        return Record;
    }

    private static string ReadJsonString(JsonElement Element, string PropertyName)
    {
        if (!Element.TryGetProperty(PropertyName, out JsonElement Value))
        {
            return string.Empty;
        }

        if (Value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        return Value.ToString().Trim();
    }

    private static string ReadJsonSku(JsonElement Element)
    {
        if (!Element.TryGetProperty("sku", out JsonElement Sku))
        {
            return string.Empty;
        }

        if (Sku.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (Sku.ValueKind == JsonValueKind.String)
        {
            return Sku.GetString() ?? string.Empty;
        }

        return Sku.GetRawText();
    }

    private static string ReadJsonTags(JsonElement Element)
    {
        if (!Element.TryGetProperty("tags", out JsonElement Tags))
        {
            return string.Empty;
        }

        if (Tags.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (Tags.ValueKind == JsonValueKind.Object)
        {
            return Tags.GetRawText();
        }

        return Tags.ToString();
    }
}
