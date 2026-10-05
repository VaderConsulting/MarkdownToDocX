# Enterprise diagram reference PoC

This folder is **reference only**. It is a JavaScript proof of concept used to port
notation, parsing, validation, and rendering into the native C# `enterprise`
diagram type.

Do not treat these files as a shipped feature. Some comments still say
`archimate`; the product keyword is `enterprise`.

Files:

* `notation.js` — freshly drawn element icons, boxes, and relationship ends
* `parser.js` — syntax accepted by the native parser (header is now `enterprise`)
* `validate.js` — relationship-table checks and nesting warnings
* `render.js` — ELK layout plus SVG emission (no `<marker>` / `<foreignObject>`)
