using System;
using System.Globalization;
using System.Collections.Generic;
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
        if (Hint.ColumnOffset.HasValue || Hint.Kind == PlacementHintKind.At)
            Value += "|" + Hint.ColumnOffset?.ToString(CultureInfo.InvariantCulture);
        if (Hint.Kind == PlacementHintKind.At)
            Value += "|" + Hint.CellRow.ToString(CultureInfo.InvariantCulture) + "|" + Hint.CellColumn.ToString(CultureInfo.InvariantCulture);
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
        if (Parts.Length > 6 && int.TryParse(Parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int Row)
            && int.TryParse(Parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int Column))
        { Hint.CellRow = Row; Hint.CellColumn = Column; }
        if (Parts.Length > 4 && int.TryParse(Parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ColumnOffset))
            Hint.ColumnOffset = ColumnOffset;
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

        if (Hint.Kind == PlacementHintKind.At)
            return " at " + Hint.CellRow.ToString(CultureInfo.InvariantCulture) + " " + Hint.CellColumn.ToString(CultureInfo.InvariantCulture);
        if (Hint.IsDiagonal && !string.IsNullOrWhiteSpace(Hint.PeerId))
        {
            string Direction = Hint.Kind switch { PlacementHintKind.NorthWest => "NW", PlacementHintKind.NorthEast => "NE",
                PlacementHintKind.SouthEast => "SE", _ => "SW" };
            return " " + Direction + " " + Hint.PeerId
                + (Hint.Offset != 0 || Hint.ColumnOffset.HasValue ? " +" + Hint.Offset.ToString(CultureInfo.InvariantCulture) : string.Empty)
                + (Hint.ColumnOffset.HasValue ? " +" + Hint.ColumnOffset.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }
        string Suffix = Hint.Kind switch
        {
            PlacementHintKind.Above when !string.IsNullOrWhiteSpace(Hint.PeerId) => " above " + Hint.PeerId,
            PlacementHintKind.Below when !string.IsNullOrWhiteSpace(Hint.PeerId) => " below " + Hint.PeerId,
            PlacementHintKind.LeftOf when !string.IsNullOrWhiteSpace(Hint.PeerId) => " left-of " + Hint.PeerId,
            PlacementHintKind.RightOf when !string.IsNullOrWhiteSpace(Hint.PeerId) => " right-of " + Hint.PeerId,
            PlacementHintKind.Push => " push " + DirectionLetter(Hint.Direction),
            _ => string.Empty
        };
        return Suffix + (Suffix.Length > 0 && Hint.Kind != PlacementHintKind.Push && Hint.Offset != 0
            ? " +" + Hint.Offset.ToString(CultureInfo.InvariantCulture) : string.Empty);
    }

    public static void WriteRouting(XElement Element, RoutingHintContract? Hint)
    {
        if (Hint is not { IsActive: true }) return;
        EnsureProperty(Element, ROUTING_KEY, string.Join("|", Hint.Leave, Hint.Enter, Hint.Travel,
            Hint.LeavePoint?.ToString(CultureInfo.InvariantCulture), Hint.EnterPoint?.ToString(CultureInfo.InvariantCulture)));
    }

    public static RoutingHintContract? ReadRouting(XElement Element)
    {
        string[]? Parts = ReadProperty(Element, ROUTING_KEY)?.Split('|');
        if (Parts is not { Length: 5 }) return null;
        bool Direction(string Text, out LayoutHintDirection? Value)
        {
            Value = null;
            if (Text.Length == 0) return true;
            if (!Enum.TryParse<LayoutHintDirection>(Text, true, out var Parsed) || !Enum.IsDefined(Parsed)) return false;
            Value = Parsed; return true;
        }
        bool Point(string Text, out int? Value)
        {
            Value = null;
            if (Text.Length == 0) return true;
            if (!int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Parsed)) return false;
            Value = Parsed; return true;
        }
        if (!Direction(Parts[0], out var Leave) || !Direction(Parts[1], out var Enter) || !Direction(Parts[2], out var Travel)
            || !Point(Parts[3], out var LeavePoint) || !Point(Parts[4], out var EnterPoint)
            || (LeavePoint.HasValue && !Leave.HasValue) || (EnterPoint.HasValue && !Enter.HasValue)) return null;
        return new() { Leave = Leave, Enter = Enter, Travel = Travel, LeavePoint = LeavePoint, EnterPoint = EnterPoint };
    }

    public static string FormatRoutingSuffix(RoutingHintContract? Hint)
    {
        if (Hint is not { IsActive: true }) return string.Empty;
        List<string> Parts = [];
        string End(LayoutHintDirection Direction, int? Step) => DirectionLetter(Direction)
            + (Step.HasValue ? "@" + Step.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        if (Hint.Leave.HasValue) Parts.Add("leave " + End(Hint.Leave.Value, Hint.LeavePoint));
        if (Hint.Enter.HasValue) Parts.Add("enter " + End(Hint.Enter.Value, Hint.EnterPoint));
        if (Hint.Travel.HasValue) Parts.Add("travel " + DirectionLetter(Hint.Travel));
        return Parts.Count == 0 ? string.Empty : " [" + string.Join(", ", Parts) + "]";
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
