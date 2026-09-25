/**
 * The DOM id a field's own wrapper carries (field-view.tsx, table-field.tsx),
 * so something outside the renderer can jump straight to it (T-34's "każda
 * pozycja [braku] jest odnośnikiem prowadzącym prosto do tego pola").
 *
 * Its own function rather than a literal template at each call site, so the
 * writer (the renderer) and the two readers (submission-gaps.ts and whatever
 * jumps to the id) cannot drift apart on the prefix.
 */
export function fieldAnchorId(fieldKey: string): string {
  return `field-${fieldKey}`;
}
