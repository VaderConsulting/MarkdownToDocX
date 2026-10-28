using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

internal static class EnterpriseStyleSvg
{
    internal static string Apply(string Svg, EnterpriseDocument Document)
    {
        EnterpriseStyles Styles = Document.Styles;
        if (!Styles.Elements.Values.Any(Style => Style.Count > 0) && Styles.Links.Count == 0 && Styles.DefaultLink.Count == 0)
        {
            return Svg;
        }

        XDocument Xml = XDocument.Parse(Svg);
        foreach (XElement? Group in Xml.Descendants().Where(Item => Item.Name.LocalName == "g" && Item.Attribute("data-id") != null))
        {
            if (!Styles.Elements.TryGetValue(Group.Attribute("data-id")!.Value, out Dictionary<string, string>? Properties))
            {
                continue;
            }

            foreach (XElement? Shape in Group.Descendants().Where(Item => Item.Name.LocalName is "rect" or "path" or "ellipse" or "polygon" or "circle" or "line"))
            {
                if (Shape.Ancestors().Any(Item => (string?)Item.Attribute("class") == "notation-icon"))
                {
                    continue;
                }

                ApplyProperties(Shape, Properties, false);
            }
            foreach (XElement? Text in Group.Descendants().Where(Item => Item.Name.LocalName == "text"))
            {
                ApplyProperties(Text, Properties, true);
            }
        }
        (EnterpriseRelationship Relationship, int Index)[] Relationships = [.. Document.Relationships.Select((Relationship, Index) => (Relationship, Index)).Where(Item => !Item.Relationship.HiddenByNesting)];
        int Position = 0;
        foreach (XElement? Group in Xml.Descendants().Where(Item => Item.Name.LocalName == "g" && Item.Attribute("data-source") != null))
        {
            if (Position >= Relationships.Length)
            {
                break;
            }

            Dictionary<string, string> Properties = new Dictionary<string, string>(Styles.DefaultLink, StringComparer.OrdinalIgnoreCase);
            if (Styles.Links.TryGetValue(Relationships[Position++].Index, out Dictionary<string, string>? Overrides))
            {
                foreach (KeyValuePair<string, string> Pair in Overrides)
                {
                    Properties[Pair.Key] = Pair.Value;
                }
            }

            foreach (XElement? Line in Group.Elements().Where(Item => Item.Attribute("data-edge-source") != null))
            {
                ApplyProperties(Line, Properties, false);
            }

            foreach (XElement? Text in Group.Descendants().Where(Item => Item.Name.LocalName == "text"))
            {
                ApplyProperties(Text, Properties, true);
            }
        }
        return Xml.ToString(SaveOptions.DisableFormatting);
    }

    private static void ApplyProperties(XElement Element, Dictionary<string, string> Properties, bool Text)
    {
        foreach (KeyValuePair<string, string> Pair in Properties)
        {
            string Key = Pair.Key.ToLowerInvariant();
            if (Key is "d" or "points" or "x" or "y" or "width" or "height" or "transform" or "rx" or "ry" or "r")
            {
                continue;
            }

            if (Text)
            {
                if (Key == "color")
                {
                    Element.SetAttributeValue("fill", Pair.Value);
                }
                else if (Key.StartsWith("font-", StringComparison.Ordinal) || Key is "opacity" or "text-decoration")
                {
                    Element.SetAttributeValue(Key, Pair.Value);
                }
            }
            else
            {
                if (Key == "color" || Key.StartsWith("font-", StringComparison.Ordinal))
                {
                    continue;
                }

                if (Key == "fill" && (string?)Element.Attribute("fill") == "none")
                {
                    continue;
                }

                Element.SetAttributeValue(Key, Pair.Value);
            }
        }
    }
}
