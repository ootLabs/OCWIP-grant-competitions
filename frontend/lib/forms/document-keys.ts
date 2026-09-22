/**
 * Field keys, generated so nobody has to type one (T-26).
 *
 * The creator's whole reason to exist is that the operator never sees JSON or
 * a technical identifier. A key still has to exist, because it is what an
 * answer, a report column and a contract placeholder are filed under, so it
 * is derived from the label the operator already typed instead.
 */
import type { FormDocument } from "./document-types";

/**
 * Same shape FormJsonReader.IsValidKey demands on the backend: lower case
 * ASCII, digits and underscores, starting with a letter, up to 64 characters.
 */
export function isValidKey(key: string): boolean {
  return /^[a-z][a-z0-9_]{0,63}$/.test(key);
}

/**
 * A key derived from an operator's label. Polish diacritics are folded to
 * their plain letter rather than dropped, because dropping them turns
 * "wkład własny" and "wklad wlasny" into the same key colliding silently.
 */
export function slugifyKey(label: string): string {
  const folded = label
    .toLowerCase()
    .replace(/ą/g, "a")
    .replace(/ć/g, "c")
    .replace(/ę/g, "e")
    .replace(/ł/g, "l")
    .replace(/ń/g, "n")
    .replace(/ó/g, "o")
    .replace(/ś/g, "s")
    .replace(/ź|ż/g, "z");

  const slug = folded
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .slice(0, 64)
    .replace(/_+$/g, "");

  if (slug.length === 0) {
    return "pole";
  }

  return /^[a-z]/.test(slug) ? slug : `pole_${slug}`.slice(0, 64);
}

/** A key not already used by `taken`, appending a counter when it collides. */
export function uniqueKey(label: string, taken: ReadonlySet<string>): string {
  const base = slugifyKey(label);

  if (!taken.has(base)) {
    return base;
  }

  for (let suffix = 2; suffix < 1000; suffix += 1) {
    const candidate = `${base.slice(0, 64 - String(suffix).length - 1)}_${suffix}`;
    if (!taken.has(candidate)) {
      return candidate;
    }
  }

  throw new Error("Nie udało się wygenerować unikalnego klucza pola.");
}

/** Every field key used anywhere in the document, sections and columns alike. */
export function allFieldKeys(document: FormDocument): Set<string> {
  const keys = new Set<string>();

  for (const section of document.sections) {
    for (const field of section.fields) {
      keys.add(field.key);
      for (const column of field.table?.columns ?? []) {
        keys.add(column.key);
      }
    }
  }

  return keys;
}
