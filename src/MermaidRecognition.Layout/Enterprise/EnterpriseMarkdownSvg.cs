using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

internal static class EnterpriseMarkdownSvg
{
    internal static string Apply(string Svg, EnterpriseDocument Document)
    {
        if (!Document.Elements.Values.Any(Item => Item.IsMarkdownLabel)
            && !Document.Groups.Values.Any(Item => Item.IsMarkdownLabel)
            && !Document.Relationships.Any(Item => Item.IsMarkdownLabel))
        {
            return Svg;
        }

        XDocument Xml = XDocument.Parse(Svg);
        foreach (XElement? Group in Xml.Descendants().Where(Item => Item.Name.LocalName == "g" && Item.Attribute("data-id") != null))
        {
            string Id = Group.Attribute("data-id")!.Value;
            if ((Document.Elements.TryGetValue(Id, out EnterpriseElement? Element) && Element.IsMarkdownLabel)
                || (Document.Groups.TryGetValue(Id, out EnterpriseGroup? Container) && Container.IsMarkdownLabel))
            {
                Format(Group);
            }
        }
        EnterpriseRelationship[] Relationships = [.. Document.Relationships.Where(Item => !Item.HiddenByNesting)];
        int Index = 0;
        foreach (XElement? Group in Xml.Descendants().Where(Item => Item.Name.LocalName == "g" && Item.Attribute("data-source") != null))
        {
            if (Index < Relationships.Length && Relationships[Index].IsMarkdownLabel)
            {
                Format(Group);
            }

            Index++;
        }
        return Xml.ToString(SaveOptions.DisableFormatting);
    }

    private static void Format(XElement Group)
    {
        bool Bold = false, Italic = false;
        foreach (XElement? Text in Group.Descendants().Where(Item => Item.Name.LocalName == "text").ToArray())
        {
            Text.SetAttributeValue(XNamespace.Xml + "space", "preserve");
            XElement[] Lines = Text.Elements().Any() ? [.. Text.Elements()] : [Text];
            foreach (XElement? Line in Lines)
            {
                string Value = Line.Value;
                List<XElement> Spans = [];
                StringBuilder Buffer = new StringBuilder();
                void Flush()
                {
                    if (Buffer.Length == 0)
                    {
                        return;
                    }

                    XElement Span = new XElement(Line.Name.Namespace + "tspan", Buffer.ToString());
                    if (Bold)
                    {
                        Span.SetAttributeValue("font-weight", "bold");
                    }

                    if (Italic)
                    {
                        Span.SetAttributeValue("font-style", "italic");
                    }

                    Spans.Add(Span);
                    Buffer.Clear();
                }
                for (int Index = 0; Index < Value.Length; Index++)
                {
                    if (Value[Index] == '\\' && Index + 1 < Value.Length && Value[Index + 1] == '*')
                    { Buffer.Append(Value[++Index]); continue; }
                    if (Value[Index] != '*') { Buffer.Append(Value[Index]); continue; }
                    Flush();
                    if (Index + 1 < Value.Length && Value[Index + 1] == '*') { Bold = !Bold; Index++; }
                    else
                    {
                        Italic = !Italic;
                    }
                }
                Flush();
                Line.ReplaceNodes(Spans);
            }
        }
    }
}
