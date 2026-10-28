using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using MermaidRecognition.Kinds.Enterprise;

namespace MermaidRecognition.Layout.Enterprise;

internal static class EnterpriseSvgConfiguration
{
    internal static string Apply(string Svg, EnterpriseDocument Document)
    {
        if (!Document.Configuration.Keys.Any(Key => Key is "themeVariables.fontFamily" or "fontFamily"
            or "themeVariables.background" or "themeVariables.fontSize" or "fontSize"))
        {
            return Svg;
        }

        XDocument Xml = XDocument.Parse(Svg);
        XElement Root = Xml.Root!;
        if (Document.Configuration.TryGetValue("themeVariables.fontFamily", out string? Family)
            || Document.Configuration.TryGetValue("fontFamily", out Family))
        {
            Root.SetAttributeValue("font-family", Family);
        }

        if (Document.Configuration.TryGetValue("themeVariables.background", out string? Background))
        {
            Root.Elements().FirstOrDefault(Element => Element.Name.LocalName == "rect")?.SetAttributeValue("fill", Background);
        }

        if (Document.Configuration.TryGetValue("themeVariables.fontSize", out string? Font)
            || Document.Configuration.TryGetValue("fontSize", out Font))
        {
            if (double.TryParse(Font.Replace("px", "", StringComparison.OrdinalIgnoreCase), NumberStyles.Float,
                CultureInfo.InvariantCulture, out double Size) && Size > 0 && Size <= 96)
            {
                // Scale the viewport and all content together, retaining measured label clearance.
                foreach (string Dimension in new[] { "width", "height" })
                {
                    Root.SetAttributeValue(Dimension, (double.Parse(Root.Attribute(Dimension)!.Value,
                        CultureInfo.InvariantCulture) * Size / 12).ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
        }
        return Xml.ToString(SaveOptions.DisableFormatting);
    }
}
