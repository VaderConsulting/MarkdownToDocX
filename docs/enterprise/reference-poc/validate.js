// Relationship-constraint validator (ArchiMate 3.1 Appendix B tables, in-scope subset).
import { RULES_31, DELTA_32 } from './rules-data.js';
import { ELEMENTS, RELATIONSHIPS } from './notation.js';

export function allowedLetters(version, srcType, tgtType) {
  if (version === '3.2' && DELTA_32[srcType] && DELTA_32[srcType][tgtType] !== undefined) return DELTA_32[srcType][tgtType];
  return (RULES_31[srcType] || {})[tgtType] || '';
}

const NESTABLE = new Set(['composition', 'aggregation', 'assignment', 'realization', 'access', 'specialization']);

export function validate(model) {
  const out = [];
  const add = (severity, line, message) => out.push({ severity, line, message });
  if (model.version === '3.2') add('info', 0, 'version 3.2: 15 Technology-layer cells use provisional 3.2 values not yet verified against the 3.2 specification text.');
  for (const r of model.relationships) {
    const s = model.elements.get(r.source), t = model.elements.get(r.target);
    const letters = allowedLetters(model.version, s.type, t.type);
    const letter = RELATIONSHIPS[r.type].letter;
    if (!letters.includes(letter)) {
      const allowed = [...letters].map((l) => Object.keys(RELATIONSHIPS).find((k) => RELATIONSHIPS[k].letter === l)).join(', ');
      add('error', r.line, `${r.type} from ${ELEMENTS[s.type].spec} "${s.label}" to ${ELEMENTS[t.type].spec} "${t.label}" is not allowed in ArchiMate ${model.version}. Allowed: ${allowed || 'none'}.`);
    }
  }
  // Nesting is a notation for a relationship the model must still contain (3.1 §5.1).
  for (const e of model.elements.values()) {
    if (!e.parent || !model.elements.has(e.parent)) continue;
    const ok = model.relationships.some((r) => r.source === e.parent && r.target === e.id && NESTABLE.has(r.type));
    if (!ok) add('warning', e.line, `"${e.id}" is nested in element "${e.parent}" but no composition/aggregation/assignment/realization/access/specialization from "${e.parent}" to "${e.id}" is declared; the nesting is ambiguous.`);
  }
  return out;
}
