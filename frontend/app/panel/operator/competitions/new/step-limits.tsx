"use client";

import { costCategoryLabels, percentageBasisLabels } from "@/app/competitions/labels";
import type { CostCategory } from "@/lib/competitions";
import type { CompetitionDraft } from "@/lib/competition-wizard/types";

import { AmountField } from "./amount-field";
import { FieldError } from "./field-error";
import { PercentField } from "./percent-field";

const ALL_COST_CATEGORIES: CostCategory[] = [
  "DirectCosts",
  "InstitutionalDevelopment",
  "IndirectCosts",
];

/**
 * Krok 1.4: Limity (docs/runbook/pola.md), "najważniejszy krok w całym
 * kreatorze, bo z niego wynikają wszystkie blokady w budżecie wniosku"
 * (T-31, poza zakresem tej karty). Cztery kwoty, każda z kwotą słownie pod
 * spodem (AmountField).
 */
export function StepLimits({
  draft,
  onChange,
  fieldErrors,
}: {
  draft: CompetitionDraft;
  onChange: (patch: Partial<CompetitionDraft>) => void;
  fieldErrors: Record<string, string[]>;
}) {
  const toggleCategory = (category: CostCategory, enabled: boolean) => {
    onChange({
      costCategories: enabled
        ? [...draft.costCategories, category]
        : draft.costCategories.filter((c) => c !== category),
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-4">
        <label className="flex flex-col gap-1 text-sm">
          Termin realizacji zadań od
          <input
            type="date"
            className="rounded-sm border border-border px-2 py-1"
            value={draft.projectStartDate}
            onChange={(event) =>
              onChange({ projectStartDate: event.target.value })
            }
          />
        </label>
        <label className="flex flex-col gap-1 text-sm">
          do
          <input
            type="date"
            className="rounded-sm border border-border px-2 py-1"
            value={draft.projectEndDate}
            onChange={(event) =>
              onChange({ projectEndDate: event.target.value })
            }
          />
          <FieldError messages={fieldErrors.projectEndDate} />
        </label>
      </div>

      <AmountField
        label="Całkowita kwota na realizację zadań (pula konkursu)"
        value={draft.totalPoolAmount}
        onChange={(value) => onChange({ totalPoolAmount: value })}
        fieldErrors={fieldErrors.totalPoolAmount}
      />

      <AmountField
        label="Minimalna dotacja na jeden wniosek"
        value={draft.minGrantAmount}
        onChange={(value) => onChange({ minGrantAmount: value })}
        fieldErrors={fieldErrors.minGrantAmount}
      />

      <AmountField
        label="Maksymalna dotacja na jeden wniosek"
        value={draft.maxGrantAmount}
        onChange={(value) => onChange({ maxGrantAmount: value })}
        fieldErrors={fieldErrors.maxGrantAmount}
      />

      <AmountField
        label="Maksymalny średni roczny przychód organizacji z trzech ostatnich zamkniętych lat"
        value={draft.maxAverageAnnualRevenue}
        onChange={(value) => onChange({ maxAverageAnnualRevenue: value })}
        fieldErrors={fieldErrors.maxAverageAnnualRevenue}
      />

      <PercentField
        label="Maksymalny procent kosztów pośrednich z dotacji"
        value={draft.maxIndirectCostPercent}
        onChange={(value) => onChange({ maxIndirectCostPercent: value })}
        fieldErrors={fieldErrors.maxIndirectCostPercent}
      />

      <PercentField
        label="Maksymalny procent kosztów rozwoju instytucjonalnego"
        value={draft.maxInstitutionalDevelopmentPercent}
        onChange={(value) =>
          onChange({ maxInstitutionalDevelopmentPercent: value })
        }
        fieldErrors={fieldErrors.maxInstitutionalDevelopmentPercent}
      />

      <fieldset className="flex flex-col gap-1 text-sm">
        <legend>Procenty liczone względem</legend>
        {(["GrantAmount", "TotalProjectValue"] as const).map((basis) => (
          <label key={basis} className="flex items-center gap-2">
            <input
              type="radio"
              name="percentageBasis"
              checked={draft.percentageBasis === basis}
              onChange={() => onChange({ percentageBasis: basis })}
            />
            {percentageBasisLabels[basis]}
          </label>
        ))}
      </fieldset>

      <label className="flex flex-col gap-1 text-sm">
        Data, do której przetwarzane będą dane osobowe
        <input
          type="date"
          className="w-48 rounded-sm border border-border px-2 py-1"
          value={draft.personalDataProcessedUntil}
          onChange={(event) =>
            onChange({ personalDataProcessedUntil: event.target.value })
          }
        />
        <FieldError messages={fieldErrors.personalDataProcessedUntil} />
      </label>

      <fieldset className="flex flex-col gap-1 text-sm">
        <legend>Kategorie kosztów w budżecie wniosku</legend>
        {ALL_COST_CATEGORIES.map((category) => (
          <label key={category} className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={draft.costCategories.includes(category)}
              onChange={(event) =>
                toggleCategory(category, event.target.checked)
              }
            />
            {costCategoryLabels[category]}
          </label>
        ))}
        <FieldError messages={fieldErrors.costCategories} />
      </fieldset>
    </div>
  );
}
