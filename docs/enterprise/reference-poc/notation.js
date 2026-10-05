// Element catalogue, colours and notation drawing for the Application + Technology layers.
// All geometry below was drawn fresh for this PoC from the *descriptions and figures* of the
// ArchiMate(R) 3.1 Specification (Appendix A "Summary of Language Notation", Chapter 5
// "Relationships"). No figure, icon file or path data was copied from the specification,
// from any modelling tool, or from any shape library.
// ArchiMate is a registered trademark of The Open Group in the United States and other countries.

// Layer fills. The specification says colours carry no formal semantics (3.1 §3.9) but its
// example figures use blue for Application and green for Technology. The values below are the
// fills measured from the Appendix A notation figure of the 3.1 specification.
export const LAYER_COLOURS = {
  application: { fill: '#AFFFFF', stroke: '#000000', label: 'Application layer' },
  technology: { fill: '#AFFFAF', stroke: '#000000', label: 'Technology layer' },
};

// aspect: 'active' | 'behaviour' | 'passive'. Corner convention (3.1 §3.9): square corners for
// structure elements, round corners for behaviour elements.
export const ELEMENTS = {
  'application-component':     { layer: 'application', aspect: 'active',    icon: 'component',     spec: 'ApplicationComponent' },
  'application-collaboration': { layer: 'application', aspect: 'active',    icon: 'collaboration', spec: 'ApplicationCollaboration' },
  'application-interface':     { layer: 'application', aspect: 'active',    icon: 'interface',     spec: 'ApplicationInterface' },
  'application-function':      { layer: 'application', aspect: 'behaviour', icon: 'function',      spec: 'ApplicationFunction' },
  'application-interaction':   { layer: 'application', aspect: 'behaviour', icon: 'interaction',   spec: 'ApplicationInteraction' },
  'application-process':       { layer: 'application', aspect: 'behaviour', icon: 'process',       spec: 'ApplicationProcess' },
  'application-event':         { layer: 'application', aspect: 'behaviour', icon: 'event',         spec: 'ApplicationEvent' },
  'application-service':       { layer: 'application', aspect: 'behaviour', icon: 'service',       spec: 'ApplicationService' },
  'data-object':               { layer: 'application', aspect: 'passive',   icon: null,            spec: 'DataObject', box: 'object' },
  'node':                      { layer: 'technology',  aspect: 'active',    icon: 'node',          spec: 'Node' },
  'device':                    { layer: 'technology',  aspect: 'active',    icon: 'device',        spec: 'Device', box: 'box3d' },
  'system-software':           { layer: 'technology',  aspect: 'active',    icon: 'systemSoftware', spec: 'SystemSoftware' },
  'technology-collaboration':  { layer: 'technology',  aspect: 'active',    icon: 'collaboration', spec: 'TechnologyCollaboration' },
  'technology-interface':      { layer: 'technology',  aspect: 'active',    icon: 'interface',     spec: 'TechnologyInterface' },
  'path':                      { layer: 'technology',  aspect: 'active',    icon: 'path',          spec: 'Path' },
  'communication-network':     { layer: 'technology',  aspect: 'active',    icon: 'network',       spec: 'CommunicationNetwork' },
  'technology-function':       { layer: 'technology',  aspect: 'behaviour', icon: 'function',      spec: 'TechnologyFunction' },
  'technology-process':        { layer: 'technology',  aspect: 'behaviour', icon: 'process',       spec: 'TechnologyProcess' },
  'technology-interaction':    { layer: 'technology',  aspect: 'behaviour', icon: 'interaction',   spec: 'TechnologyInteraction' },
  'technology-event':          { layer: 'technology',  aspect: 'behaviour', icon: 'event',         spec: 'TechnologyEvent' },
  'technology-service':        { layer: 'technology',  aspect: 'behaviour', icon: 'service',       spec: 'TechnologyService' },
  'artifact':                  { layer: 'technology',  aspect: 'passive',   icon: 'artifact',      spec: 'Artifact' },
};

// Relationship types, spec table letters and line/arrowhead notation (3.1 Chapter 5 / Appendix A.3).
export const RELATIONSHIPS = {
  composition:    { letter: 'c', line: 'solid',  start: 'diamondFilled', end: null },
  aggregation:    { letter: 'g', line: 'solid',  start: 'diamondHollow', end: null },
  assignment:     { letter: 'i', line: 'solid',  start: 'dot',           end: 'arrowFilled' },
  realization:    { letter: 'r', line: 'dotted', start: null,            end: 'triangleHollow' },
  serving:        { letter: 'v', line: 'solid',  start: null,            end: 'arrowOpen' },
  access:         { letter: 'a', line: 'dotted', start: null,            end: 'arrowOpenSmall' },
  influence:      { letter: 'n', line: 'dashed', start: null,            end: 'arrowOpen' },
  triggering:     { letter: 't', line: 'solid',  start: null,            end: 'arrowFilled' },
  flow:           { letter: 'f', line: 'dashed', start: null,            end: 'arrowFilled' },
  specialization: { letter: 's', line: 'solid',  start: null,            end: 'triangleHollow' },
  association:    { letter: 'o', line: 'solid',  start: null,            end: null },
};
export const LETTER_TO_REL = Object.fromEntries(Object.entries(RELATIONSHIPS).map(([k, v]) => [v.letter, k]));

export const DASH = { solid: null, dotted: '1.5 3', dashed: '6 4' };

const f = (n) => Math.round(n * 100) / 100;

// ---- Corner icons: drawn in a 20 x 14 box whose top-left is (x, y). Black 1px strokes. ----
export function iconSvg(icon, x, y, fill) {
  const s = 'stroke="#000" stroke-width="1" stroke-linejoin="round"';
  const n = (v) => f(v);
  switch (icon) {
    case 'component': // body with two tabs protruding on the left side
      return `<g ${s} fill="${fill}"><rect x="${n(x + 6)}" y="${n(y + 1)}" width="12" height="12"/>` +
        `<rect x="${n(x + 3)}" y="${n(y + 3.5)}" width="6" height="2.5"/><rect x="${n(x + 3)}" y="${n(y + 8)}" width="6" height="2.5"/></g>`;
    case 'collaboration': // two overlapping circles
      return `<g ${s} fill="none"><circle cx="${n(x + 7.5)}" cy="${n(y + 7)}" r="5"/><circle cx="${n(x + 12.5)}" cy="${n(y + 7)}" r="5"/></g>`;
    case 'interface': // circle on a short stem to the left ("lollipop")
      return `<g ${s} fill="none"><line x1="${n(x + 2)}" y1="${n(y + 7)}" x2="${n(x + 9)}" y2="${n(y + 7)}"/><circle cx="${n(x + 13.5)}" cy="${n(y + 7)}" r="4.5"/></g>`;
    case 'function': // upward chevron: pointed top, notched bottom
      return `<path ${s} fill="none" d="M${n(x + 5)},${n(y + 5)} L${n(x + 10)},${n(y + 1)} L${n(x + 15)},${n(y + 5)} L${n(x + 15)},${n(y + 13)} L${n(x + 10)},${n(y + 9)} L${n(x + 5)},${n(y + 13)} Z"/>`;
    case 'interaction': // circle split into two separated halves
      return `<g ${s} fill="none"><path d="M${n(x + 9)},${n(y + 1.5)} A5.5,5.5 0 0 0 ${n(x + 9)},${n(y + 12.5)} Z"/>` +
        `<path d="M${n(x + 11)},${n(y + 1.5)} A5.5,5.5 0 0 1 ${n(x + 11)},${n(y + 12.5)} Z"/></g>`;
    case 'process': // block arrow pointing right
      return `<path ${s} fill="none" d="M${n(x + 2)},${n(y + 5)} H${n(x + 11)} V${n(y + 2)} L${n(x + 18)},${n(y + 7)} L${n(x + 11)},${n(y + 12)} V${n(y + 9)} H${n(x + 2)} Z"/>`;
    case 'event': // notched left end, rounded right end
      return `<path ${s} fill="none" d="M${n(x + 3)},${n(y + 2)} H${n(x + 12)} A5,5 0 0 1 ${n(x + 12)},${n(y + 12)} H${n(x + 3)} L${n(x + 6.5)},${n(y + 7)} Z"/>`;
    case 'service': // stadium
      return `<rect ${s} fill="none" x="${n(x + 2)}" y="${n(y + 3)}" width="16" height="8" rx="4" ry="4"/>`;
    case 'node': // small 3D box
      return `<g ${s} fill="none"><rect x="${n(x + 4)}" y="${n(y + 4)}" width="10" height="9"/>` +
        `<path d="M${n(x + 4)},${n(y + 4)} L${n(x + 7)},${n(y + 1)} H${n(x + 17)} V${n(y + 10)} L${n(x + 14)},${n(y + 13)} M${n(x + 14)},${n(y + 4)} L${n(x + 17)},${n(y + 1)}"/></g>`;
    case 'device': // screen with a flared base
      return `<g ${s} fill="none"><rect x="${n(x + 4)}" y="${n(y + 1)}" width="12" height="8" rx="1.5"/>` +
        `<path d="M${n(x + 6)},${n(y + 9)} L${n(x + 3)},${n(y + 13)} H${n(x + 17)} L${n(x + 14)},${n(y + 9)}"/></g>`;
    case 'systemSoftware': // disc with a second, offset arc behind it
      return `<g ${s} fill="none"><path d="M${n(x + 9)},${n(y + 3.2)} A5,5 0 1 1 ${n(x + 16.6)},${n(y + 9.3)}"/>` +
        `<circle cx="${n(x + 9.5)}" cy="${n(y + 8)}" r="5"/></g>`;
    case 'path': // dotted line between two open angle brackets
      return `<g ${s} fill="none"><path d="M${n(x + 6)},${n(y + 3)} L${n(x + 2)},${n(y + 7)} L${n(x + 6)},${n(y + 11)} M${n(x + 14)},${n(y + 3)} L${n(x + 18)},${n(y + 7)} L${n(x + 14)},${n(y + 11)}"/>` +
        `<line x1="${n(x + 5)}" y1="${n(y + 7)}" x2="${n(x + 15)}" y2="${n(y + 7)}" stroke-dasharray="1.2 1.8"/></g>`;
    case 'network': // four filled dots on a parallelogram, joined by lines
      { const p = [[x + 6, y + 2], [x + 17, y + 2], [x + 14, y + 12], [x + 3, y + 12]];
        return `<g ${s}><path fill="none" d="M${p.map((q) => q.map(n).join(',')).join(' L')} Z"/>` +
          p.map((q) => `<circle cx="${n(q[0])}" cy="${n(q[1])}" r="1.8" fill="#000"/>`).join('') + '</g>'; }
    case 'artifact': // page with folded top-right corner
      return `<path ${s} fill="none" d="M${n(x + 5)},${n(y + 1)} H${n(x + 11)} L${n(x + 15)},${n(y + 5)} V${n(y + 13)} H${n(x + 5)} Z M${n(x + 11)},${n(y + 1)} V${n(y + 5)} H${n(x + 15)}"/>`;
    default:
      return '';
  }
}

// ---- Element boxes ("rectangle with icon" notation) ----
export function elementBoxSvg(type, x, y, w, h) {
  const el = ELEMENTS[type];
  const { fill, stroke } = LAYER_COLOURS[el.layer];
  const s = `fill="${fill}" stroke="${stroke}" stroke-width="1"`;
  let out = '';
  if (el.box === 'box3d') {
    const d = 8; // depth of the 3D bevel
    out += `<path ${s} d="M${f(x)},${f(y + d)} L${f(x + d)},${f(y)} H${f(x + w)} V${f(y + h - d)} L${f(x + w - d)},${f(y + h)} H${f(x)} Z"/>`;
    out += `<path fill="none" stroke="${stroke}" stroke-width="1" d="M${f(x)},${f(y + d)} H${f(x + w - d)} V${f(y + h)} M${f(x + w - d)},${f(y + d)} L${f(x + w)},${f(y)}"/>`;
    out += iconSvg(el.icon, x + w - d - 23, y + d + 3, fill);
    return out;
  }
  if (el.box === 'object') { // passive: rectangle with a header band
    out += `<rect ${s} x="${f(x)}" y="${f(y)}" width="${f(w)}" height="${f(h)}"/>`;
    out += `<line stroke="${stroke}" stroke-width="1" x1="${f(x)}" y1="${f(y + 12)}" x2="${f(x + w)}" y2="${f(y + 12)}"/>`;
    return out;
  }
  const r = el.aspect === 'behaviour' ? 10 : 0;
  out += `<rect ${s} x="${f(x)}" y="${f(y)}" width="${f(w)}" height="${f(h)}" rx="${r}" ry="${r}"/>`;
  out += iconSvg(el.icon, x + w - 23, y + 3, fill);
  return out;
}

// ---- Relationship ends, emitted as explicit paths (no <marker>, so rasterisers such as
// Svg.Skia and Word's SVG import draw them reliably). (px,py)=tip on the element boundary,
// (ux,uy)=unit vector pointing from the line towards the tip. ----
export function endSvg(kind, px, py, ux, uy) {
  if (!kind) return { svg: '', inset: 0 };
  const vx = -uy, vy = ux; // perpendicular
  const P = (a, b) => `${f(px - ux * a + vx * b)},${f(py - uy * a + vy * b)}`;
  switch (kind) {
    case 'diamondFilled':
    case 'diamondHollow': {
      const L = 16, W = 5.5;
      return { inset: L, svg: `<path d="M${P(0, 0)} L${P(L / 2, W)} L${P(L, 0)} L${P(L / 2, -W)} Z" fill="${kind === 'diamondFilled' ? '#000' : '#fff'}" stroke="#000" stroke-width="1"/>` };
    }
    case 'dot':
      return { inset: 3.5, svg: `<circle cx="${f(px - ux * 3.5)}" cy="${f(py - uy * 3.5)}" r="3.5" fill="#000"/>` };
    case 'arrowFilled': {
      const L = 10, W = 4.5;
      return { inset: L, svg: `<path d="M${P(0, 0)} L${P(L, W)} L${P(L, -W)} Z" fill="#000" stroke="#000" stroke-width="1"/>` };
    }
    case 'triangleHollow': {
      const L = 14, W = 7;
      return { inset: L, svg: `<path d="M${P(0, 0)} L${P(L, W)} L${P(L, -W)} Z" fill="#fff" stroke="#000" stroke-width="1"/>` };
    }
    case 'arrowOpen': {
      const L = 11, W = 5.5;
      return { inset: 0, svg: `<path d="M${P(L, W)} L${P(0, 0)} L${P(L, -W)}" fill="none" stroke="#000" stroke-width="1"/>` };
    }
    case 'arrowOpenSmall': {
      const L = 7, W = 3.5;
      return { inset: 0, svg: `<path d="M${P(L, W)} L${P(0, 0)} L${P(L, -W)}" fill="none" stroke="#000" stroke-width="1"/>` };
    }
    case 'halfArrow': {
      const L = 10, W = 5;
      return { inset: 0, svg: `<path d="M${P(L, W)} L${P(0, 0)}" fill="none" stroke="#000" stroke-width="1"/>` };
    }
    default:
      return { svg: '', inset: 0 };
  }
}
