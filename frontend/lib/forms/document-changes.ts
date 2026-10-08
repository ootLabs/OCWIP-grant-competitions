/**
 * What changed in the kreator's document against the one it started from:
 * the competition it was copied from, or the version last published. The
 * report bets on "kopia z poprawkami" as OCWIP's way of working, and in that
 * way of working this list is the most useful thing on the screen: it is what
 * the operator checks before publishing, and what the announcement can say
 * changed this year.
 */
import type { FormDocument, FormField } from "./document-types";

export type ChangeKind = "new" | "changed" | "removed";

export interface FieldChange {
  readonly fieldKey: string;
  readonly sectionKey: string;
  readonly sectionTitle: string;
  readonly label: string;
  readonly kind: ChangeKind;
  /** Polish, one short phrase per changed property: "nazwa", "limit znaków". */
  readonly what: readonly string[];
  /** The label it had before, when the name is one of the changes. */
  readonly wasLabel?: string;
}

/** Each comparable property with the words the operator reads for it. */
const PROPERTIES: readonly [string, (field: FormField) => unknown][] = [
  ["nazwa", (f) => f.label],
  ["podpowiedź", (f) => f.help],
  ["wymagalność", (f) => f.required],
  ["limit znaków", (f) => [f.minLength ?? null, f.maxLength ?? null]],
  ["zakres wartości", (f) => [f.minValue ?? null, f.maxValue ?? null]],
  ["odpowiedzi do wyboru", (f) => f.options ?? null],
  ["treść oświadczenia", (f) => f.statementText ?? null],
  ["warunek widoczności", (f) => f.visibleWhen ?? null],
  ["limity kwot", (f) => f.limits ?? null],
  ["tabela", (f) => f.table ?? null],
  ["wydruk", (f) => f.printed],
];

function index(document: FormDocument) {
  const fields = new Map<string, { field: FormField; sectionKey: string; sectionTitle: string }>();
  for (const section of document.sections) {
    for (const field of section.fields) {
      fields.set(field.key, { field, sectionKey: section.key, sectionTitle: section.title });
    }
  }
  return fields;
}

const same = (left: unknown, right: unknown) => JSON.stringify(left) === JSON.stringify(right);

/** Changes in the order of the current document, removed fields last. */
export function documentChanges(baseline: FormDocument | null, current: FormDocument): FieldChange[] {
  if (baseline === null) {
    return [];
  }

  const before = index(baseline);
  const after = index(current);
  const changes: FieldChange[] = [];

  for (const [key, { field, sectionKey, sectionTitle }] of after) {
    const was = before.get(key);
    if (was === undefined) {
      changes.push({ fieldKey: key, sectionKey, sectionTitle, label: field.label, kind: "new", what: [] });
      continue;
    }

    const what = PROPERTIES.filter(([, read]) => !same(read(was.field), read(field))).map(([name]) => name);
    if (what.length > 0) {
      changes.push({
        fieldKey: key,
        sectionKey,
        sectionTitle,
        label: field.label,
        kind: "changed",
        what,
        wasLabel: was.field.label !== field.label ? was.field.label : undefined,
      });
    }
  }

  for (const [key, { field, sectionKey, sectionTitle }] of before) {
    if (!after.has(key)) {
      changes.push({ fieldKey: key, sectionKey, sectionTitle, label: field.label, kind: "removed", what: [] });
    }
  }

  return changes;
}

/** "3 zmiany", "1 zmiana", "5 zmian": Polish plural for the count on the bar. */
export function changesLabel(count: number): string {
  const lastTwo = count % 100;
  const last = count % 10;
  if (count === 1) {
    return "1 zmiana";
  }
  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) {
    return `${count} zmiany`;
  }
  return `${count} zmian`;
}
