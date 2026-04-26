#r "nuget: Naiad, 0.1.2"
using MermaidSharp;
using System.Xml.Linq;
var svg = Mermaid.Render("""
classDiagram
class Animal {
+String name
+move()
}
class Dog {
+bark()
}
Animal <|-- Dog
""", RenderOptions.Default);
var root=XDocument.Parse(svg).Root;
Console.WriteLine(root.ToString(SaveOptions.DisableFormatting).Substring(0, Math.Min(1000, root.ToString(SaveOptions.DisableFormatting).Length)));
