#r "nuget: Naiad, 0.1.2"
using MermaidSharp;
var source = """
flowchart TD
    A["Markdown"] --> B["Detect Mermaid fence"]
    B --> C[Render SVG]
    B --> D["Render normal Markdown"]
    C --> E["Embed in DOCX"]
""";
var svg = Mermaid.Render(source, RenderOptions.Default);
Console.WriteLine(svg.Substring(0, Math.Min(svg.Length, 8000)));
Console.WriteLine("foreignObject count: " + System.Text.RegularExpressions.Regex.Matches(svg, "<foreignObject").Count);
