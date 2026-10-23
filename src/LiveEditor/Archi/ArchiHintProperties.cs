using System;
using System.Globalization;
using System.Xml.Linq;
using MermaidRecognition.Contract;
using MermaidRecognition.Kinds.Enterprise;

namespace LiveEditor.Archi;

/// <summary>
/// Stores and restores Mermaid placement/routing hints as Archi element properties.
/// </summary>
internal static class ArchiHintProperties
{
    public const string PLACEMENT_KEY = "naiad.placementHint";

    public const string ROUTING_KEY = "naiad.routingHint";

    public static void WritePlacement(XElement ElementNode, PlacementHintContract? Hint)
    {
        if (Hint == null || ElementNode == null)
        {
            return;
        }

        string Value = Hint.Kind.ToString()
            + "|"
            + (Hint.PeerId ?? string.Empty)
            + "|"
            + Hint.Offset.ToString(CultureInfo.InvariantCulture)
            + "|"
            + (Hint.Direction?.ToString() ?? string.Empty);
        EnsureProperty(ElementNode, PLACEMENT_KEY, Value);
    }

    public static PlacementHintContract? ReadPlacement(XElement ElementNode)
    {
        string? Value = ReadProperty(ElementNode, PLACEMENT_KEY);
        if (string.IsNullOrWhiteSpace(Value))
        {
            return null;
        }

        string[] Parts = Value.Split('|');
        if (Parts.Length < 1 || !Enum.TryParse(Parts[0], ignoreCase: true, out PlacementHintKind Kind))
        {
            return null;
        }

        PlacementHintContract Hint = new PlacementHintContract { Kind = Kind };
        if (Parts.Length > 1 && !string.IsNullOrWhiteSpace(Parts[1]))
        {
            Hint.PeerId = Parts[1];
        }

        if (Parts.Length > 2 && int.TryParse(Parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int Offset))
        {
            Hint.Offset = Offset;
        }

        if (Parts.Length > 3 && Enum.TryParse(Parts[3], ignoreCase: true, out LayoutHintDirection Direction))
        {
            Hint.Direction = Direction;
        }

        return Hint;
    }

    public static string FormatPlacementSuffix(PlacementHintContract? Hint)
    {
        if (Hint == null)
        {
            return string.Empty;
        }

        return Hint.Kind switch
        {
            PlacementHintKind.Above when !string.IsNullOrWhiteSpace(Hint.PeerId) => " above " + Hint.PeerId,
            PlacementHintKind.Below when !string.IsNullOrWhiteSpace(Hint.PeerId) => " below " + Hint.PeerId,
            PlacementHintKind.LeftOf when !string.IsNullOrWhiteSpace(Hint.PeerId) => " left-of " + Hint.PeerId,
            PlacementHintKind.RightOf when !string.IsNullOrWhiteSpace(Hint.PeerId) => " right-of " + Hint.PeerId,
            PlacementHintKind.Push => " push " + DirectionLetter(Hint.Direction),
            _ => string.Empty
        };
    }

    private static string DirectionLetter(LayoutHintDirection? Direction)
    {
        if (!Direction.HasValue)
        {
            return "N";
        }

        return Direction.Value switch
        {
            LayoutHintDirection.North => "N",
            LayoutHintDirection.South => "S",
            LayoutHintDirection.East => "E",
            _ => "W"
        };
    }

    private static void EnsureProperty(XElement ElementNode, string Key, string Value)
    {
        XElement? Properties = ElementNode.Element("properties");
        if (Properties == null)
        {
            Properties = new XElement("properties");
            ElementNode.Add(Properties);
        }

        foreach (XElement Existing in Properties.Elements("property"))
        {
            if (string.Equals((string?)Existing.Attribute("key"), Key, StringComparison.Ordinal))
            {
                Existing.SetAttributeValue("value", Value);
                return;
            }
        }

        Properties.Add(new XElement("property", new XAttribute("key", Key), new XAttribute("value", Value)));
    }

    private static string? ReadProperty(XElement ElementNode, string Key)
    {
        XElement? Properties = ElementNode.Element("properties");
        if (Properties == null)
        {
            return null;
        }

        foreach (XElement Property in Properties.Elements("property"))
        {
            if (string.Equals((string?)Property.Attribute("key"), Key, StringComparison.Ordinal))
            {
                return (string?)Property.Attribute("value");
            }
        }

        return null;
    }
}
