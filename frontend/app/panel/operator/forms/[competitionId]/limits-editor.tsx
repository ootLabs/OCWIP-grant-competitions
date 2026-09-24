"use client";

import { limitBasisCandidates } from "@/lib/forms/document-candidates";
import {
  COMPETITION_PERCENT_SETTINGS,
  competitionBasis,
  type FormDocument,
  type FormLimit,
  type LimitKind,
} from "@/lib/forms/document-types";
import { COMPETITION_BASIS_LABELS, LIMIT_KIND_LABELS } from "@/lib/forms/labels";

/** The value of the percentage source picker that means "type a number". */
const FIXED_PERCENT = "";

/**
 * The rules a numeric field is checked against (D12): kind, plus what it is
 * measured against, both from dropdowns. This is also how the operator marks
 * the budget's summary column as "the one the grant ceiling watches": adding
 * a "nie więcej niż kwota" limit against "Maksymalna dotacja na wniosek" here
 * is that instruction, without a separate switch pretending to be a simpler
 * idea than the limit it sets (card T-26, "tabela budżetu").
 */
export function LimitsEditor({
  document,
  sectionKey,
  fieldKey,
  limits,
  onChange,
}: {
  document: FormDocument;
  sectionKey: string;
  fieldKey: string;
  limits: readonly FormLimit[];
  onChange: (limits: FormLimit[]) => void;
}) {
  const candidates = limitBasisCandidates(document, sectionKey, fieldKey);

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm">Limity</span>
      {limits.length === 0 ? <p className="text-sm">Brak limitów na tym polu.</p> : null}
      <ul className="flex flex-col gap-2">
        {limits.map((limit, index) => (
          <li key={index} className="flex flex-wrap items-center gap-2">
            <select
              className="rounded-sm border border-border px-2 py-1 text-sm"
              value={limit.kind}
              onChange={(event) => {
                const kind = event.target.value as LimitKind;
                const next = [...limits];
                next[index] =
                  kind === "maxPercentOf"
                    ? { ...limit, kind, percent: limit.percentFrom ? undefined : (limit.percent ?? 10) }
                    : { kind, basis: limit.basis };
                onChange(next);
              }}
            >
              {(Object.keys(LIMIT_KIND_LABELS) as LimitKind[]).map((kind) => (
                <option key={kind} value={kind}>
                  {LIMIT_KIND_LABELS[kind]}
                </option>
              ))}
            </select>

            {limit.kind === "maxPercentOf" ? (
              <select
                className="rounded-sm border border-border px-2 py-1 text-sm"
                aria-label="Skąd procent"
                value={limit.percentFrom ?? FIXED_PERCENT}
                onChange={(event) => {
                  // The threshold of a cost table is the competition's to set
                  // (T-31); a number typed here is the one wrong next year.
                  const source = event.target.value;
                  const next = [...limits];
                  next[index] =
                    source === FIXED_PERCENT
                      ? { kind: limit.kind, basis: limit.basis, percent: 10 }
                      : { kind: limit.kind, basis: limit.basis, percentFrom: source };
                  onChange(next);
                }}
              >
                <option value={FIXED_PERCENT}>Stały procent</option>
                {COMPETITION_PERCENT_SETTINGS.map((setting) => (
                  <option key={setting} value={competitionBasis(setting)}>
                    {COMPETITION_BASIS_LABELS[setting]}
                  </option>
                ))}
              </select>
            ) : null}

            {limit.kind === "maxPercentOf" && limit.percentFrom === undefined ? (
              <input
                type="number"
                className="w-20 rounded-sm border border-border px-2 py-1 text-sm"
                aria-label="Procent"
                value={limit.percent ?? 0}
                onChange={(event) => {
                  const next = [...limits];
                  next[index] = { ...limit, percent: Number(event.target.value) };
                  onChange(next);
                }}
              />
            ) : null}

            <select
              className="flex-1 rounded-sm border border-border px-2 py-1 text-sm"
              value={limit.basis}
              onChange={(event) => {
                const next = [...limits];
                next[index] = { ...limit, basis: event.target.value };
                onChange(next);
              }}
            >
              <option value="">(wybierz)</option>
              {candidates.map((candidate) => (
                <option key={candidate.key} value={candidate.key}>
                  {candidate.label}
                </option>
              ))}
            </select>

            <button
              type="button"
              className="text-sm underline"
              onClick={() => onChange(limits.filter((_, i) => i !== index))}
            >
              Usuń
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        className="self-start text-sm underline"
        onClick={() =>
          onChange([...limits, { kind: "maxAmount", basis: candidates[0]?.key ?? "" }])
        }
      >
        Dodaj limit
      </button>
    </div>
  );
}
