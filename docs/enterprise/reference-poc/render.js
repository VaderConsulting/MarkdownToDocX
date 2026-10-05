// Layout (dagre) + SVG rendering. Output uses only <rect>/<path>/<circle>/<line>/<text>, no
// <foreignObject> and no <marker>, so it survives rasterisers like Svg.Skia (used by markdown2docx).
import ELK from 'elkjs/lib/elk.bundled.js';
import { ELEMENTS, RELATIONSHIPS, DASH, elementBoxSvg, endSvg } from './notation.js';

const FONT = 12, LINE_H = 15, CHAR_W = 6.6, MIN_W = 120, MAX_CHARS = 22, EL_H = 55;
const esc = (s) => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const f = (n) => Math.round(n * 100) / 100;

function wrap(label) {
  const words = label.split(/\s+/); const lines = []; let cur = '';
  for (const w of words) {
    if (cur && (cur + ' ' + w).length > MAX_CHARS) { lines.push(cur); cur = w; } else cur = cur ? cur + ' ' + w : w;
  }
  if (cur) lines.push(cur);
  return lines;
}

function layerRank(type) { return ELEMENTS[type].layer === 'application' ? 0 : 1; }
// Layout-only edge orientation: "consumer/whole/realized above provider/part/realizer", and
// Application above Technology. Semantic direction is untouched.
function layoutOrientation(model, r) {
  const s = model.elements.get(r.source), t = model.elements.get(r.target);
  const ls = layerRank(s.type), lt = layerRank(t.type);
  if (ls !== lt) return ls < lt ? [r.source, r.target] : [r.target, r.source];
  if (['serving', 'realization', 'assignment'].includes(r.type)) return [r.target, r.source];
  return [r.source, r.target];
}

export async function layout(model) {
  // ELK layered (the same engine family Mermaid ships as @mermaid-js/layout-elk); handles nested
  // elements/groups and edges that cross hierarchy levels.
  const parents = new Set([...model.elements.values(), ...model.groups.values()].map((c) => c.parent).filter(Boolean));
  const key = (id) => (model.elements.has(id) ? 'e:' : 'g:') + id;
  const nodes = new Map();
  for (const grp of model.groups.values()) nodes.set('g:' + grp.id, { id: 'g:' + grp.id, kind: 'group', ref: grp, children: [], layoutOptions: { 'elk.padding': '[top=32,left=14,bottom=14,right=14]' } });
  for (const e of model.elements.values()) {
    const lines = wrap(e.label);
    const is3d = ELEMENTS[e.type].box === 'box3d';
    const w = Math.max(MIN_W, Math.max(...lines.map((l) => l.length)) * CHAR_W + 40) + (is3d ? 28 : 0);
    const h = Math.max(EL_H, lines.length * LINE_H + 26) + (is3d ? 8 : 0);
    const n = { id: 'e:' + e.id, kind: 'element', ref: e, lines, children: [] };
    if (parents.has(e.id)) { n.compound = true; n.layoutOptions = { 'elk.padding': '[top=36,left=14,bottom=14,right=14]' }; } else { n.width = w; n.height = h; }
    nodes.set(n.id, n);
  }
  const root = { id: 'root', children: [], edges: [], layoutOptions: {
    'elk.algorithm': 'layered', 'elk.direction': model.direction === 'LR' ? 'RIGHT' : 'DOWN',
    'elk.hierarchyHandling': 'INCLUDE_CHILDREN', 'elk.edgeRouting': 'ORTHOGONAL',
    'elk.layered.spacing.nodeNodeBetweenLayers': '55', 'elk.spacing.nodeNode': '35', 'elk.spacing.edgeNode': '20',
    'elk.spacing.edgeEdge': '12', 'elk.edgeLabels.placement': 'CENTER', 'elk.layered.nodePlacement.strategy': 'NETWORK_SIMPLEX',
    'elk.padding': '[top=20,left=20,bottom=20,right=20]' } };
  for (const c of [...model.groups.values(), ...model.elements.values()]) {
    const n = nodes.get(key(c.id) === 'e:' + c.id && model.elements.get(c.id) === c ? 'e:' + c.id : 'g:' + c.id);
    (c.parent ? nodes.get(key(c.parent)) : root).children.push(n);
  }
  const drawn = [];
  model.relationships.forEach((r, i) => {
    const child = model.elements.get(r.target);
    r.hiddenByNesting = child.parent === r.source && ['composition', 'aggregation', 'assignment', 'realization', 'access', 'specialization'].includes(r.type);
    if (r.hiddenByNesting) return;
    const [a, b] = layoutOrientation(model, r);
    r.reversedForLayout = a !== r.source;
    const lbl = edgeLabel(r);
    const edge = { id: 'r' + i, sources: ['e:' + a], targets: ['e:' + b], rel: r };
    if (lbl) edge.labels = [{ text: lbl, width: lbl.length * 6 + 8, height: 14 }];
    root.edges.push(edge);
    drawn.push(r);
  });
  const elk = new ELK();
  const out = await elk.layout(root);
  // absolute positions
  const abs = new Map(); abs.set('root', { x: 0, y: 0 });
  const walk = (n, ox, oy) => { for (const c of n.children || []) { const ax = ox + c.x, ay = oy + c.y; abs.set(c.id, { x: ax, y: ay }); Object.assign(nodes.get(c.id), { ax, ay, width: c.width, height: c.height }); walk(c, ax, ay); } };
  walk(out, 0, 0);
  for (const e of out.edges) {
    const r = e.rel || root.edges.find((x) => x.id === e.id).rel;
    const o = abs.get(e.container || 'root') || { x: 0, y: 0 };
    const sec = e.sections[0];
    let pts = [sec.startPoint, ...(sec.bendPoints || []), sec.endPoint].map((p) => [p.x + o.x, p.y + o.y]);
    if (r.reversedForLayout) pts.reverse();
    r._pts = pts;
    r._label = e.labels && e.labels[0] ? [e.labels[0].x + o.x + e.labels[0].width / 2, e.labels[0].y + o.y + e.labels[0].height / 2] : null;
  }
  return { nodes: [...nodes.values()], width: out.width, height: out.height, drawn };
}


function edgeLabel(r) {
  let s = r.label || '';
  if (r.type === 'influence' && r.strength) s = s ? `${s} (${r.strength})` : r.strength;
  return s;
}


export async function renderSvg(model, opts = {}) {
  const L = await layout(model);
  const titleH = model.title ? 30 : 0;
  const W = Math.ceil(L.width + 10), H = Math.ceil(L.height + titleH + 10);
  const parts = [];
  parts.push(`<svg xmlns="http://www.w3.org/2000/svg" ${opts.id ? `id="${esc(opts.id)}" ` : ''}width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" font-family="Arial, Helvetica, sans-serif" font-size="${FONT}">`);
  parts.push(`<rect x="0" y="0" width="${W}" height="${H}" fill="#ffffff"/>`);
  if (model.title) parts.push(`<text x="${W / 2}" y="20" text-anchor="middle" font-size="15" font-weight="bold">${esc(model.title)}</text>`);
  parts.push(`<g transform="translate(0,${titleH})">`);
  const depth = (n) => { let d = 0, p = n.ref.parent; while (p) { d++; p = (model.elements.get(p) || model.groups.get(p))?.parent; } return d; };
  const nodes = [...L.nodes].sort((a, b) => depth(a) - depth(b));
  for (const n of nodes) {
    const x = n.ax, y = n.ay;
    if (n.kind === 'group') {
      const tabW = Math.max(60, n.ref.label.length * CHAR_W + 16);
      parts.push(`<g class="group" data-id="${esc(n.ref.id)}"><path d="M${f(x)},${f(y + 18)} V${f(y)} H${f(x + tabW)} V${f(y + 18)}" fill="none" stroke="#000" stroke-dasharray="5 3"/>` +
        `<rect x="${f(x)}" y="${f(y + 18)}" width="${f(n.width)}" height="${f(n.height - 18)}" fill="none" stroke="#000" stroke-dasharray="5 3"/>` +
        `<text x="${f(x + 6)}" y="${f(y + 13)}">${esc(n.ref.label)}</text></g>`);
      continue;
    }
    const e = n.ref;
    parts.push(`<g class="element ${e.type}" data-id="${esc(e.id)}">${elementBoxSvg(e.type, x, y, n.width, n.height)}`);
    const is3d = ELEMENTS[e.type].box === 'box3d';
    const lines = n.lines, cx = x + (is3d ? (n.width - 30) / 2 : n.width / 2);
    const isObj = ELEMENTS[e.type].box === 'object';
    const top = n.compound ? y + 20 : (isObj ? y + 12 + (n.height - 12) / 2 : y + n.height / 2 + (is3d ? 4 : 0)) - ((lines.length - 1) * LINE_H) / 2 + 4;
    lines.forEach((l, k) => parts.push(`<text x="${f(cx)}" y="${f(top + k * LINE_H)}" text-anchor="middle">${esc(l)}</text>`));
    parts.push('</g>');
  }
  for (const r of L.drawn) parts.push(relationshipSvg(r, r._pts));
  parts.push('</g></svg>');
  return parts.join('\n');
}

function relationshipSvg(r, pts) {
  const spec = RELATIONSHIPS[r.type];
  let start = spec.start, end = spec.end;
  if (r.type === 'access') {
    start = r.accessType === 'read' || r.accessType === 'readwrite' ? 'arrowOpenSmall' : null;
    end = r.accessType === 'write' || r.accessType === 'readwrite' ? 'arrowOpenSmall' : null;
  }
  if (r.type === 'association' && r.directed) end = 'halfArrow';
  const unit = (a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], L = Math.hypot(dx, dy) || 1; return [dx / L, dy / L]; };
  const p = pts.map((q) => q.slice());
  const n = p.length;
  const ue = unit(p[n - 2], p[n - 1]);
  const us = unit(p[1], p[0]);
  const es = endSvg(end, p[n - 1][0], p[n - 1][1], ue[0], ue[1]);
  const ss = endSvg(start, p[0][0], p[0][1], us[0], us[1]);
  p[n - 1] = [p[n - 1][0] - ue[0] * es.inset, p[n - 1][1] - ue[1] * es.inset];
  p[0] = [p[0][0] - us[0] * ss.inset, p[0][1] - us[1] * ss.inset];
  const dash = DASH[spec.line];
  let out = `<g class="relationship ${r.type}" data-source="${esc(r.source)}" data-target="${esc(r.target)}">`;
  out += `<path d="M${p.map((q) => `${f(q[0])},${f(q[1])}`).join(' L')}" fill="none" stroke="#000" stroke-width="1"${dash ? ` stroke-dasharray="${dash}"` : ''}/>`;
  out += ss.svg + es.svg;
  const label = edgeLabel(r);
  if (label) {
    const [lx, ly] = r._label || [(pts[0][0] + pts[n - 1][0]) / 2, (pts[0][1] + pts[n - 1][1]) / 2];
    const w = label.length * 6 + 8;
    out += `<rect x="${f(lx - w / 2)}" y="${f(ly - 8)}" width="${f(w)}" height="15" fill="#fff" fill-opacity="0.85"/><text x="${f(lx)}" y="${f(ly + 4)}" text-anchor="middle" font-size="11">${esc(label)}</text>`;
  }
  return out + '</g>';
}
