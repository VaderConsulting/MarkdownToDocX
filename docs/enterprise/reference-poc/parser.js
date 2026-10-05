// Parser for the proposed `archimate` Mermaid diagram syntax (PoC, hand-written).
// A production version would be a Langium grammar in @mermaid-js/parser style; see the design doc.
import { ELEMENTS, RELATIONSHIPS, LETTER_TO_REL } from './notation.js';

const SPEC_NAME_TO_TYPE = Object.fromEntries(Object.entries(ELEMENTS).map(([k, v]) => [v.spec.toLowerCase(), k]));
export function normaliseType(word) {
  const w = word.toLowerCase();
  if (ELEMENTS[w]) return w;
  return SPEC_NAME_TO_TYPE[w.replace(/-/g, '')] || null;
}

const ID = '[A-Za-z_][\\w.]*';
const STR = '"((?:[^"\\\\]|\\\\.)*)"';
const RE_ELEMENT = new RegExp(`^([A-Za-z-]+)\\s+(${ID})(?:\\s+${STR})?(?:\\s+in\\s+(${ID}))?$`);
const RE_GROUP = new RegExp(`^group\\s+(${ID})(?:\\s+${STR})?(?:\\s+in\\s+(${ID}))?$`);
// a -serving-> b "label"   |  a -v-> b   |  a -access(read)-> b  | a -influence(+)-> b
const RE_REL = new RegExp(`^(${ID})\\s+-([A-Za-z]+)(?:\\(([^)]*)\\))?->\\s+(${ID})(?:\\s+${STR})?$`);

export class ParseError extends Error {
  constructor(message, line) { super(`line ${line}: ${message}`); this.line = line; }
}

export function parse(text) {
  const model = { version: '3.1', direction: 'TB', title: '', elements: new Map(), groups: new Map(), relationships: [], diagnostics: [] };
  const lines = text.replace(/\r\n?/g, '\n').split('\n');
  let seenHeader = false;
  lines.forEach((raw, i) => {
    const ln = i + 1;
    const line = raw.replace(/%%.*$/, '').trim();
    if (!line) return;
    if (!seenHeader) {
      if (!/^archimate(-beta)?$/i.test(line)) throw new ParseError('expected diagram header "archimate"', ln);
      seenHeader = true; return;
    }
    let m;
    if ((m = /^title\s+(.+)$/.exec(line))) { model.title = m[1]; return; }
    if ((m = /^version\s+(3\.1|3\.2)$/.exec(line))) { model.version = m[1]; return; }
    if ((m = /^direction\s+(TB|LR)$/i.exec(line))) { model.direction = m[1].toUpperCase(); return; }
    if ((m = RE_GROUP.exec(line))) {
      model.groups.set(m[1], { id: m[1], label: unq(m[2]) ?? m[1], parent: m[3] || null, line: ln }); return;
    }
    if ((m = RE_REL.exec(line))) {
      const [, src, kw, qual, tgt, label] = m;
      const type = RELATIONSHIPS[kw.toLowerCase()] ? kw.toLowerCase() : LETTER_TO_REL[kw.toLowerCase()];
      if (!type) throw new ParseError(`unknown relationship type "${kw}"`, ln);
      const rel = { source: src, target: tgt, type, label: unq(label) ?? '', line: ln };
      if (qual !== undefined) {
        const q = qual.trim().toLowerCase();
        if (type === 'access' && ['read', 'write', 'readwrite', 'access'].includes(q)) rel.accessType = q;
        else if (type === 'influence' && /^[+-]{1,2}$|^[+-]?\d+$/.test(q)) rel.strength = qual.trim();
        else if (type === 'association' && q === 'directed') rel.directed = true;
        else throw new ParseError(`qualifier "(${qual})" is not valid for ${type}`, ln);
      }
      if (type === 'access' && !rel.accessType) rel.accessType = 'write'; // spec default notation: arrow towards the object
      model.relationships.push(rel); return;
    }
    if ((m = RE_ELEMENT.exec(line))) {
      const type = normaliseType(m[1]);
      if (!type) throw new ParseError(`unknown element type "${m[1]}" (in scope: Application and Technology layer elements)`, ln);
      if (model.elements.has(m[2])) throw new ParseError(`duplicate element id "${m[2]}"`, ln);
      model.elements.set(m[2], { id: m[2], type, label: unq(m[3]) ?? m[2], parent: m[4] || null, line: ln }); return;
    }
    throw new ParseError(`cannot parse "${line}"`, ln);
  });
  if (!seenHeader) throw new ParseError('empty diagram', 1);
  for (const r of model.relationships) {
    for (const end of ['source', 'target']) {
      if (!model.elements.has(r[end])) throw new ParseError(`relationship ${end} "${r[end]}" is not a declared element`, r.line);
    }
  }
  for (const c of [...model.elements.values(), ...model.groups.values()]) {
    if (c.parent && !model.groups.has(c.parent) && !model.elements.has(c.parent)) throw new ParseError(`"in ${c.parent}": no such group or element`, c.line);
  }
  return model;
}

function unq(s) { return s === undefined ? undefined : s.replace(/\\(.)/g, '$1'); }
