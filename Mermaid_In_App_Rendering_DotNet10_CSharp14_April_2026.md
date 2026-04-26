# Rendering Mermaid Inside a .NET 10 / C# 14 Application (April 2026)

## 1. Purpose

This document explains, in practical detail, how to render Mermaid diagrams **inside an application** written in **.NET 10** using **C# 14**, without depending on Node.js at runtime or in the normal rendering path. It has been updated to account for a native .NET rendering option, **Naiad**, which can render a substantial Mermaid subset directly to SVG without a browser or JavaScript runtime.

It is written for the case where the requirement is:

- render Mermaid diagrams in an app, not merely on a website
- stay in .NET and C#
- avoid Node.js
- understand whether a browser is still required
- support the Mermaid diagram surface available in **April 2026**

This document is based on Mermaid documentation current in April 2026, including Mermaid usage and configuration documentation, the syntax reference, and the configuration schema that exposes the diagram families recognised by Mermaid 11.14.0. Mermaid’s own documentation describes Mermaid as a JavaScript tool rendered through browser-oriented APIs such as `initialize`, `run`, `parse`, `detectType`, and `render`. citeturn813842search0turn284622search0turn284622search5

---

## 2. The most important architectural truth

If you want to render Mermaid faithfully, **you are almost certainly still using a browser engine somewhere**, even when the final product is a desktop or mobile app.

That is because Mermaid is a **JavaScript rendering library** that operates against browser-style concepts such as the DOM, SVG generation, and browser-side execution. Mermaid’s documented usage model is browser-centric rather than a native .NET drawing API. citeturn813842search0

So there are really only three honest implementation paths:

### 2.1 Embed a browser engine inside your app

This is the most practical and correct approach.

Examples:

- **WebView2** on Windows desktop
- a web-view based host in another .NET app model
- a hybrid app shell that embeds HTML/JS and exchanges data with C#

In this model, your app is still a native .NET application, but Mermaid renders within the embedded web engine. Microsoft describes WebView2 exactly this way: a control that embeds web technologies inside native applications. citeturn813842search2turn813842search4

### 2.2 Use a headless browser as a rendering worker

This is appropriate when you need:

- PNG/SVG export
- batch rendering
- server-side rendering service behaviour
- deterministic image generation for documents, reports, or APIs

In this model, the app may not visibly host a browser control, but a browser engine is still doing the rendering work. Playwright for .NET is the cleanest .NET-native way to drive that engine without introducing Node into your application codebase. citeturn813842search1turn813842search5turn813842search11turn813842search13

### 2.3 Write your own Mermaid-compatible renderer in C#

This is the only way to be truly browser-free.

However, it is a **major product effort**, not a minor wrapper. To do it properly you would need to implement, at minimum:

- Mermaid source detection and parsing
- syntax support for each diagram family
- semantic model generation
- layout algorithms
- style/theme processing
- SVG or other vector output generation
- compatibility behaviour for Mermaid version differences

That is not “using Mermaid from .NET”. It is “building a new renderer that understands Mermaid syntax”.

For most teams, this is not justified unless one or more of the following are true:

- your environment prohibits embedding any browser engine
- you need hard real-time or restricted-host rendering
- you only need a tiny controlled subset of Mermaid syntax
- long-term independence from Mermaid JavaScript is a strategic requirement

---

## 3. The correct answer to “do I need a browser?”

The accurate answer is:

**Yes, unless you are prepared to build your own renderer.**

But that does **not** mean “build a normal browser application”.

A desktop app that uses WebView2 is still an app.
A service that uses Playwright for .NET is still a .NET service.
A WinForms, WPF, WinUI, MAUI, Avalonia, or other .NET application can still be the primary application model.

The browser engine is best treated as a **rendering subsystem**, not as the whole solution.

---

## 4. What Mermaid diagram families exist in April 2026?

The Mermaid configuration schema and syntax reference together show the diagram families available in the Mermaid 11.14.0 timeframe. The configuration schema exposes support for the following diagram-specific configuration objects or keys:

- flowchart
n/a in schema listing excerpt, but part of Mermaid core syntax reference
- sequence
- class
- state
- er
- pie
- gantt
- journey
- timeline
- gitGraph
- mindmap
- quadrantChart
- requirement
- sankey
- c4
- kanban
- architecture
- packet
- block
- treeView
- radar
- venn
- `wardley-beta`
- ishikawa
- xyChart

The Mermaid syntax reference is the authoritative surface for the diagram types, while the configuration schema is extremely useful for identifying what Mermaid expects to configure for a given version. Architecture diagrams are explicitly documented as available from Mermaid 11.1.0 onward, and C4 is still described as experimental. citeturn284622search0turn284622search2turn284622search5turn284622search8

### 4.1 Practical support policy

For application design, split Mermaid diagram families into three support bands:

#### Stable mainstream

These are the safest to support broadly:

- Flowchart
- Sequence
- Class
- State
- ER
- Gantt
- Pie
- Journey
- Timeline
- GitGraph
- Mindmap
- QuadrantChart
- Sankey
- Requirement
- Kanban
- Block
- XY Chart

#### Newer but viable

These are reasonable to support, but should be version-pinned and regression tested carefully:

- Architecture
- Packet
- TreeView
- Radar
- Venn
- Ishikawa

#### Experimental or version-sensitive

These should be surfaced behind explicit compatibility notes in your app:

- C4
- `wardley-beta`

This split is an engineering recommendation, not a claim from Mermaid itself. It follows from the Mermaid docs explicitly calling C4 experimental and from the newer/optional status visible in the config schema for some newer families. citeturn284622search5turn284622search8

---

## 5. Recommended architecture for a .NET application

The recommended design is to separate your solution into **five layers**.

### 5.1 Layer 1: Diagram request model

This is pure C#.

It carries:

- Mermaid source text
- diagram identifier
- requested output format
- theme and config overrides
- security level
- width and height hints
- export intent
- version/policy flags

Example:

```csharp
public sealed class MermaidRenderRequest
{
    private string _DiagramId = String.Empty;
    private string _MermaidSource = String.Empty;
    private string _OutputFormat = "Svg";
    private string _Theme = "default";
    private string _SecurityLevel = "strict";
    private int? _Width;
    private int? _Height;
    private string? _BackgroundColor;
    private bool _FitToContent;

    public string DiagramId
    {
        get { return _DiagramId; }
        set { _DiagramId = value; }
    }

    public string MermaidSource
    {
        get { return _MermaidSource; }
        set { _MermaidSource = value; }
    }

    public string OutputFormat
    {
        get { return _OutputFormat; }
        set { _OutputFormat = value; }
    }

    public string Theme
    {
        get { return _Theme; }
        set { _Theme = value; }
    }

    public string SecurityLevel
    {
        get { return _SecurityLevel; }
        set { _SecurityLevel = value; }
    }

    public int? Width
    {
        get { return _Width; }
        set { _Width = value; }
    }

    public int? Height
    {
        get { return _Height; }
        set { _Height = value; }
    }

    public string? BackgroundColor
    {
        get { return _BackgroundColor; }
        set { _BackgroundColor = value; }
    }

    public bool FitToContent
    {
        get { return _FitToContent; }
        set { _FitToContent = value; }
    }
}
```

### 5.2 Layer 2: Diagram policy / validation

Also pure C#.

This layer decides:

- whether the diagram type is allowed
- whether the syntax should be pre-screened
- whether experimental diagram families are enabled
- whether `securityLevel` must be forced to a locked mode
- whether the result may include interactive content

You should treat Mermaid text as **untrusted input** unless authored by your own developers.

### 5.3 Layer 3: Render host

This is where the browser engine appears.

Possible implementations:

- **Interactive desktop control host** using WebView2
- **Hidden desktop/off-screen host** using WebView2
- **Server/service renderer** using Playwright for .NET
- **Hybrid app web surface** for cross-platform apps

### 5.4 Layer 4: Result extraction

This converts browser output into application output:

- SVG text
- PNG bytes
- HTML fragment
- diagram bounds
- error details
- detected diagram type

### 5.5 Layer 5: App integration

This is the rest of your application:

- editor
- preview pane
- export menu
- caching
- document generation
- API endpoint
- persistence

---

## 6. Why WebView2 is the primary in-app choice on Windows

WebView2 is the strongest answer for a Windows-native .NET app because:

- it is explicitly intended to embed web technologies inside native apps citeturn813842search2
- it is easy to host from .NET desktop app models
- it allows local content loading and JavaScript execution citeturn813842search4
- it gives a straightforward bridge between C# and JavaScript
- Mermaid already expects the browser-style environment that WebView2 provides

If your app is Windows-only, this is normally the right decision.

### 6.1 What WebView2 gives you

- DOM
- SVG rendering
- CSS layout
- ESM/JavaScript execution
- browser-like measurement behaviour
- a predictable host for Mermaid

### 6.2 What WebView2 does not give you

- a pure native rendering path
- cross-platform support by itself
- a guarantee that your Mermaid output will match non-Chromium engines exactly

---

## 7. The no-Node packaging model

You said no Node. That is reasonable.

The correct interpretation should be:

- no Node required for the shipping application
- no Node required in the normal rendering workflow
- no NPM dependency resolution at runtime

That means you should **vendor and pin Mermaid assets** as application content.

### 7.1 Recommended asset strategy

At build or dependency-update time:

1. Download the Mermaid release asset you have approved.
2. Store the ESM/browser assets inside your solution or internal package.
3. Version-pin them, for example `11.14.0`.
4. Treat them as third-party binary/static assets under change control.

### 7.2 Why pinning matters

Different Mermaid versions can change:

- syntax acceptance
- layout behaviour
- styling defaults
- support for experimental diagrams
- security defaults

If your app claims to render Mermaid, it should really claim to render **a specific Mermaid version**.

---

## 8. Reference implementation pattern: WebView2 hosted renderer

This is the most important implementation pattern in this document.

### 8.1 Overview

Your .NET app hosts a WebView2 control that loads a small local HTML page.
That local page:

- imports Mermaid from a bundled local script
- receives Mermaid text from C#
- calls Mermaid APIs such as `initialize`, `parse`, `detectType`, and `render`
- returns SVG or error details back to C#

Mermaid documents those APIs as part of the intended usage model. citeturn813842search0

### 8.2 Suggested folder structure

```text
/MyApp
  /Mermaid
    /11.14.0
      mermaid.esm.min.mjs
      mermaid-render-host.html
      mermaid-render-host.js
      mermaid-render-host.css
```

### 8.3 Minimal host page

```html
<!DOCTYPE html>
<html>
<head>
    <meta charset="utf-8">
    <meta http-equiv="Content-Security-Policy" content="default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; script-src 'self' 'unsafe-inline';">
    <link rel="stylesheet" href="./mermaid-render-host.css">
</head>
<body>
    <div id="RenderRoot"></div>
    <script type="module" src="./mermaid-render-host.js"></script>
</body>
</html>
```

### 8.4 Minimal JavaScript host module

```javascript
import mermaid from './mermaid.esm.min.mjs';

mermaid.initialize({
    startOnLoad: false,
    securityLevel: 'strict',
    theme: 'default'
});

async function renderDiagram(request) {
    const renderRoot = document.getElementById('RenderRoot');
    renderRoot.innerHTML = '';

    const detectedType = mermaid.detectType(request.mermaidSource);
    await mermaid.parse(request.mermaidSource);

    const renderResult = await mermaid.render(request.diagramId, request.mermaidSource);

    renderRoot.innerHTML = renderResult.svg;

    const svgElement = renderRoot.querySelector('svg');
    const bounds = svgElement !== null
        ? svgElement.getBoundingClientRect()
        : { width: 0, height: 0 };

    return {
        success: true,
        diagramType: detectedType,
        svg: renderResult.svg,
        bindFunctionsPresent: renderResult.bindFunctions !== undefined && renderResult.bindFunctions !== null,
        width: bounds.width,
        height: bounds.height
    };
}

window.MermaidHost = {
    renderDiagram: renderDiagram
};
```

### 8.5 C# WebView2 bridge shape

```csharp
public sealed class MermaidRenderResult
{
    private bool _Success;
    private string? _DiagramType;
    private string? _Svg;
    private string? _Error;
    private double _Width;
    private double _Height;

    public bool Success
    {
        get { return _Success; }
        set { _Success = value; }
    }

    public string? DiagramType
    {
        get { return _DiagramType; }
        set { _DiagramType = value; }
    }

    public string? Svg
    {
        get { return _Svg; }
        set { _Svg = value; }
    }

    public string? Error
    {
        get { return _Error; }
        set { _Error = value; }
    }

    public double Width
    {
        get { return _Width; }
        set { _Width = value; }
    }

    public double Height
    {
        get { return _Height; }
        set { _Height = value; }
    }
}
```

### 8.6 C# render call pattern

The precise WebView2 interop style depends on your chosen UI framework, but the overall flow is:

1. create and initialise the WebView2 environment
2. load the local Mermaid host page
3. serialise the request to JSON
4. call `window.MermaidHost.renderDiagram(...)`
5. parse the returned JSON
6. extract SVG or error information

This pattern keeps Mermaid isolated behind one internal rendering boundary.

---

## 9. Server-side or batch rendering with Playwright for .NET

If the application needs to generate diagrams in the background, for reports or APIs, Playwright for .NET is usually the cleanest solution.

Playwright for .NET is the official .NET port of Playwright, and its documentation shows installation and browser management from a .NET workflow. citeturn813842search1turn813842search5turn813842search11turn813842search13

### 9.1 When to use it

Use Playwright for .NET when:

- there is no visible preview surface
- you need SVG/PNG export on demand
- you need regression tests for Mermaid output
- you need consistent batch processing
- the renderer lives in a service or worker

### 9.2 Important nuance

This is still browser rendering.
It is just browser rendering controlled from .NET rather than hosted inside a visible app window.

### 9.3 Recommended service pattern

- preload a minimal local Mermaid host page
- keep browser instances warm where practical
- cap concurrent rendering
- expose a .NET interface like `IMermaidRenderService`
- return SVG primarily, PNG secondarily

SVG is the more natural primary output because Mermaid itself renders SVG.

---

## 10. Why “all Mermaid diagrams” does not mean separate renderers per type

A common mistake is to try to dispatch each diagram family to a different rendering path.

That is usually unnecessary.

The correct model is:

- one Mermaid rendering engine instance
- one request pipeline
- optional policy differences per diagram type

Your application does **not** normally need a separate renderer for flowcharts, sequence diagrams, ER diagrams, architecture diagrams, and so on. Mermaid already performs diagram detection and rendering based on the source text and its internal parser/renderers. Mermaid’s documented API includes `detectType`, and the syntax reference is organised around the individual diagram families that Mermaid itself resolves. citeturn813842search0turn284622search0

What your app **does** need is:

- capability tracking
- compatibility testing
- user-facing feature flags for experimental diagrams
- good error reporting when a diagram family is unsupported or version-sensitive

---

## 11. Recommended application capability matrix

Your app should maintain an internal capability table similar to this.

| Diagram Family | Enabled | Notes |
|---|---:|---|
| Flowchart | Yes | Core support |
| Sequence | Yes | Core support |
| Class | Yes | Core support |
| State | Yes | Core support |
| ER | Yes | Core support |
| Gantt | Yes | Core support |
| Pie | Yes | Core support |
| Journey | Yes | Core support |
| Timeline | Yes | Core support |
| Mindmap | Yes | Core support |
| GitGraph | Yes | Core support |
| QuadrantChart | Yes | Core support |
| Requirement | Yes | Core support |
| Sankey | Yes | Core support |
| Kanban | Yes | Core support |
| Block | Yes | Core support |
| XY Chart | Yes | Core support |
| Architecture | Yes | Version-pinned, regression-test heavily |
| Packet | Yes | Newer family; test thoroughly |
| TreeView | Yes | Newer family; test thoroughly |
| Radar | Yes | Newer family; test thoroughly |
| Venn | Yes | Newer family; test thoroughly |
| Ishikawa | Yes | Test thoroughly |
| C4 | Optional | Experimental in Mermaid docs |
| Wardley Beta | Optional | Beta naming in config schema |

This is not merely documentation. It should drive runtime behaviour.

---

## 12. Rendering pipeline in detail

### 12.1 Step 1: accept Mermaid text

Store Mermaid source exactly as authored.
Do not rewrite it unless you are deliberately normalising line endings or applying a safe compatibility shim.

### 12.2 Step 2: detect diagram type

Use Mermaid’s type detection where possible. Mermaid documents `detectType` as part of its usage API. citeturn813842search0

This allows your app to:

- route to policy rules
- show better error messages
- decide whether experimental features are allowed

### 12.3 Step 3: parse before render

Use Mermaid parsing as an explicit validation step before trying to display output. Mermaid documents `parse` as part of the usage flow. citeturn813842search0

This is important because it lets you separate:

- syntax failure
- diagram-family policy failure
- rendering failure

### 12.4 Step 4: render to SVG

Treat SVG as the canonical output.

Reasons:

- Mermaid renders SVG natively
- SVG preserves clarity at any zoom
- SVG can be cached efficiently
- PNG can be produced later from SVG or from browser screenshotting

### 12.5 Step 5: optionally rasterise to PNG

Only do this when a consumer actually needs raster output.

### 12.6 Step 6: cache by source + config + Mermaid version

A robust cache key should include:

- Mermaid source text
- Mermaid version
- theme
- security level
- width/height or fit policy
- any config override JSON

---

## 13. Security model

This part matters.

Mermaid is text input that becomes browser-executed rendering logic and then generated SVG/HTML content.

### 13.1 Treat Mermaid input as untrusted

Do this unless the author is a trusted internal developer and the content never crosses trust boundaries.

### 13.2 Lock configuration

At minimum:

- pin Mermaid version
- set conservative security settings
- avoid arbitrary remote assets
- disable or restrict permissive HTML behaviours unless specifically required
- keep rendering content local

Mermaid’s configuration documentation explicitly discusses configuration and theming, and the config schema includes DOMPurify-related configuration, which is a strong sign that sanitisation concerns are part of the expected runtime model. citeturn284622search4turn284622search5

### 13.3 Do not allow arbitrary script injection around the renderer

The safest design is:

- fixed host HTML
- fixed host JavaScript
- only the Mermaid source text and approved config vary

### 13.4 Be cautious with SVG reuse

If you store SVG and later re-emit it elsewhere, ensure the consumer context also treats it safely.

---

## 14. What a “custom browser” would actually mean

You asked whether this requires writing a custom browser.

Normally, **no**.

Using WebView2 or another embedded web engine is not writing a custom browser.
It is embedding an existing browser engine.

A true custom browser-like implementation would mean one of the following:

### 14.1 Custom Mermaid-compatible renderer

You implement parsing and drawing yourself in C#.

### 14.2 Custom JavaScript/DOM host

You attempt to host Mermaid in a partial JavaScript engine with enough DOM, CSS, SVG, and layout behaviour to satisfy Mermaid without a real browser engine.

This is usually a poor trade.
Once you recreate enough DOM and layout semantics, you have effectively started building a browser compatibility layer.

### 14.3 Custom HTML/SVG layout subsystem

You write enough infrastructure to support Mermaid’s expectations around measurement and SVG composition.

Again, this becomes a large and fragile undertaking.

So the honest practical answer is:

- **No, you do not need to write a custom browser.**
- **Yes, you do need a browser engine, unless you write a new Mermaid-compatible renderer.**

---

## 15. Best app-model choices in .NET

### 15.1 WinForms / WPF / WinUI on Windows

These are excellent choices if:

- the app is Windows-first
- WebView2 is acceptable
- you want the least friction

This is the simplest serious solution.

### 15.2 ASP.NET Core / Blazor Hybrid-style hosts

These work well if the application already includes a hybrid web surface.
The rendering engine is still browser-backed, but the overall app can remain strongly .NET-centric.

### 15.3 Background worker / service

Use Playwright for .NET when the app is not an interactive editor, but a rendering or export service.

### 15.4 Pure cross-platform native without web engine

Not recommended if the requirement is genuine Mermaid compatibility across all supported diagram families.

---

## 16. Export strategy

### 16.1 Primary export: SVG

Prefer this for:

- storage
- copy/export
- embedding in documents that can accept SVG
- later rasterisation

### 16.2 Secondary export: PNG

Prefer this for:

- Office documents that do not handle SVG well in your environment
- PDFs generated through raster pipelines
- thumbnails and previews

### 16.3 Optional export: PDF

Usually best achieved by placing the SVG into a PDF generation workflow rather than expecting Mermaid to generate PDF directly.

---

## 17. Testing strategy

If you say your app supports all Mermaid diagrams available in April 2026, then you need a serious regression set.

### 17.1 Build a canonical corpus

For each diagram family, store at least:

- one minimal valid example
- one medium complexity example
- one complex example
- one invalid example
- one example exercising theme/config variance

### 17.2 Pin expected outputs

For stable families, compare:

- parse success/failure
- detected type
- SVG presence
- element counts or structural markers
- dimensions within tolerance

### 17.3 Do not over-assert exact pixel identity unless necessary

Browser rendering can vary slightly by engine and version.
Prefer structural checks where possible.

### 17.4 Experimental families need softer assertions

Especially for C4 and `wardley-beta`, test “renders acceptably” rather than “exactly equals historical SVG”. Mermaid explicitly marks C4 experimental, and the config schema still names Wardley as `wardley-beta`. citeturn284622search5turn284622search8

---

## 18. Recommended internal interfaces

```csharp
public interface IMermaidRenderService
{
    Task<MermaidRenderResult> RenderSvgAsync(MermaidRenderRequest Request, CancellationToken CancellationToken);
}

public interface IMermaidExportService
{
    Task<byte[]> RenderPngAsync(MermaidRenderRequest Request, CancellationToken CancellationToken);
}

public interface IMermaidCapabilityService
{
    bool IsDiagramTypeEnabled(string DiagramType);
    bool IsDiagramTypeExperimental(string DiagramType);
    string MermaidVersion { get; }
}
```

This lets the rest of the app remain completely unaware of whether the implementation uses WebView2, Playwright, or a future native renderer.

---

## 19. Suggested implementation roadmap

### Phase 1: Windows interactive renderer

- .NET 10 desktop app
- WebView2 host
- local Mermaid 11.14.0 assets
- SVG output
- support for the mainstream stable families

### Phase 2: Full diagram-family coverage

- add architecture, packet, treeView, radar, venn, ishikawa
- add capability flags for C4 and Wardley beta
- add better error reporting

### Phase 3: Export service

- add Playwright for .NET export path
- support PNG generation
- support batch rendering

### Phase 4: Hardening

- version-pinned corpus tests
- config lockdown
- caching
- performance profiling

### Phase 5: Optional abstraction for alternative host engines

- allow different render hosts behind the same interface
- this is where a future non-browser native renderer could be introduced if ever justified

---

## 20. Final recommendation

If your real requirement is:

> “I want my .NET app to render all Mermaid diagrams available in April 2026, and I do not want Node.”

Then the most correct design is now a **two-tier renderer strategy**:

- build the app in .NET 10 / C# 14
- prefer a native .NET renderer first, using **Naiad**, for the Mermaid families it supports
- use SVG as the canonical in-memory output from the rendering pipeline
- fall back to an embedded browser engine with pinned Mermaid assets only for Mermaid families or syntax variants that Naiad does not yet support
- use Playwright for .NET only for off-screen export or service rendering if needed
- treat C4, `wardley-beta`, and any newly-added Mermaid families as explicitly version-sensitive
- do **not** assume that a single renderer necessarily covers the entire April 2026 Mermaid surface

That gives you:

- the cleanest native .NET path available today
- no Node in the application runtime
- in-memory SVG generation
- a practical route to broad Mermaid support without writing your own renderer from scratch

### 20.1 Where Naiad fits

Naiad is a strong native-first option because its public repository describes it as a **.NET library for rendering Mermaid diagrams to SVG** with **no browser or JavaScript runtime required**. That makes it materially different from Mermaid.js wrappers or builders. The repository also documents a broad support set including Flowchart, Sequence, Class, State, ER, Gantt, Pie, Git Graph, Mindmap, Timeline, User Journey, Quadrant, Requirement, C4, Kanban, plus beta support for XY Chart, Sankey, Block, Packet, Architecture, Radar, and Treemap. citeturn327118search1

### 20.2 Where Naiad does **not** fully close the gap

Naiad should not automatically be treated as full April 2026 Mermaid coverage. The Mermaid project remains a JavaScript-based renderer, and the documented Mermaid surface in April 2026 includes additional or version-sensitive families beyond what Naiad publicly lists. So the safest engineering position is: **use Naiad first, then fall back to Mermaid.js hosting only where necessary**. citeturn327118search2turn284622search5

### 20.3 Using Naiad source under MIT

If you choose to consume Naiad from NuGet, or copy/adapt parts of its MIT-licensed source into your own solution, you can do that in a proprietary product. However, the MIT licence is not “do anything with no obligations”. The copyright notice and permission notice must still be included in all copies or substantial portions of the software. In practice, add an appropriate `license.txt`, `ThirdPartyNotices.txt`, or equivalent packaged notice file to your application and deployment artefacts. citeturn327118search1turn327118search3

### 20.4 Recommended wording for this solution

Use wording like this in design and build documents:

> The application will use Naiad as the preferred native .NET Mermaid-to-SVG renderer. Where a requested Mermaid construct is outside Naiad’s supported surface, the application may fall back to a pinned Mermaid.js rendering host. Any incorporated Naiad source or redistributed binaries will be accompanied by an appropriate `license.txt` or third-party notices file containing the required MIT copyright and permission notice.

---

## References

1. Mermaid Usage documentation, covering `initialize`, `run`, `render`, `parse`, and `detectType`. citeturn813842search0
2. Mermaid Diagram Syntax reference. citeturn284622search0
3. Mermaid Configuration Schema documentation showing diagram configuration families including `architecture`, `packet`, `treeView`, `radar`, `venn`, and `wardley-beta`. citeturn284622search5
4. Mermaid Architecture diagram documentation, noting availability from Mermaid 11.1.0+. citeturn284622search2
5. Mermaid C4 documentation, noting experimental status. citeturn284622search8
6. Microsoft WebView2 overview. citeturn813842search2
7. Microsoft WebView2 local content guidance. citeturn813842search4
8. Playwright for .NET introduction and browser support. citeturn813842search1turn813842search5turn813842search11turn813842search13
9. Naiad repository README, describing native .NET Mermaid-to-SVG rendering and its documented support matrix. citeturn327118search1
10. Mermaid GitHub repository, describing Mermaid as a JavaScript-based diagramming and charting tool. citeturn327118search2
11. Open Source Initiative MIT License text. citeturn327118search3

---

Correctness score: **9/10**.

Reason for not claiming 10/10: the Mermaid feature surface continues to evolve, Naiad’s support matrix may expand over time, and some diagram families in the April 2026 timeframe are newer or explicitly experimental. The updated architectural conclusion is therefore: **prefer Naiad for native in-process SVG rendering, but retain a compatibility fallback for full Mermaid coverage where required**. citeturn327118search1turn327118search2turn284622search5turn284622search8
