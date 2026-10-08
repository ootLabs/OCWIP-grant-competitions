"use client";

import type { FieldChange } from "@/lib/forms/document-changes";

/**
 * Every change against the form this one started from, as one table the
 * operator reads before publishing, and could copy into the announcement as
 * "co się zmieniło we wniosku w tym roku".
 */
export function ChangesList({
  changes,
  baselineLabel,
  onOpen,
}: {
  changes: readonly FieldChange[];
  baselineLabel: string;
  onOpen: (sectionKey: string, fieldKey: string) => void;
}) {
  if (changes.length === 0) {
    return <p className="text-sm text-text-muted">Formularz jest taki sam jak {baselineLabel}.</p>;
  }

  return (
    <div className="relative overflow-x-auto rounded-md border border-border-muted">
      <table className="w-full border-collapse text-sm">
        <caption className="sr-only">Zmiany względem: {baselineLabel}</caption>
        <thead>
          <tr className="text-left text-xs uppercase tracking-wide text-text-muted">
            <th scope="col" className="border-b border-border-muted px-3 py-2">Pole</th>
            <th scope="col" className="border-b border-border-muted px-3 py-2">Sekcja</th>
            <th scope="col" className="border-b border-border-muted px-3 py-2">Co się zmieniło</th>
          </tr>
        </thead>
        <tbody>
          {changes.map((change) => (
            <tr key={`${change.kind}-${change.fieldKey}`}>
              <td className="border-b border-border-muted px-3 py-2">
                {change.kind === "removed" ? (
                  <s>{change.label}</s>
                ) : (
                  <button
                    type="button"
                    className="text-left underline"
                    onClick={() => onOpen(change.sectionKey, change.fieldKey)}
                  >
                    {change.label || "(bez etykiety)"}
                  </button>
                )}
              </td>
              <td className="border-b border-border-muted px-3 py-2">{change.sectionTitle}</td>
              <td className="border-b border-border-muted px-3 py-2">
                {change.kind === "new" ? "nowe pole" : null}
                {change.kind === "removed" ? "usunięte" : null}
                {change.kind === "changed" ? change.what.join(", ") : null}
                {change.wasLabel ? <span className="block text-xs text-text-muted">Wcześniej: {change.wasLabel}</span> : null}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
