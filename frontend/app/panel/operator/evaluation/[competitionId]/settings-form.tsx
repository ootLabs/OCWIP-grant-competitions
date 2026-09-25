"use client";

import { useId, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import {
  saveEvaluationSettings,
  type EvaluationSettings,
  type EvaluationSettingsRequest,
} from "@/lib/operator-evaluation";

const toNumber = (value: string): number | null =>
  value.trim() === "" ? null : Number(value);
const toText = (value: number | string | null | undefined): string =>
  value === null || value === undefined ? "" : String(value);

/**
 * How applications of the competition are evaluated (T-39 settings, report
 * step 5.0). An empty threshold means none; an empty divergence percentage
 * switches the warning off.
 */
export function SettingsForm({
  competitionId,
  settings,
  onSaved,
}: {
  competitionId: string;
  settings: EvaluationSettings;
  onSaved: (settings: EvaluationSettings) => void;
}) {
  const [evaluators, setEvaluators] = useState(
    toText(settings.evaluatorsPerApplication),
  );
  const [aggregation, setAggregation] = useState(settings.scoreAggregation);
  const [threshold, setThreshold] = useState(toText(settings.meritThreshold));
  const [includesStrategic, setIncludesStrategic] = useState(
    settings.thresholdIncludesStrategic,
  );
  const [divergence, setDivergence] = useState(
    toText(settings.divergenceThresholdPercent),
  );
  const [state, setState] = useState<{
    saving: boolean;
    message: string | null;
  }>({ saving: false, message: null });
  const ids = {
    evaluators: useId(),
    aggregation: useId(),
    threshold: useId(),
    divergence: useId(),
  };

  async function save() {
    setState({ saving: true, message: null });
    const request: EvaluationSettingsRequest = {
      evaluatorsPerApplication: Number(evaluators),
      scoreAggregation: aggregation,
      meritThreshold: toNumber(threshold),
      thresholdIncludesStrategic: includesStrategic,
      divergenceThresholdPercent: toNumber(divergence),
    };

    try {
      const saved = await saveEvaluationSettings(competitionId, request);
      onSaved(saved);
      setState({ saving: false, message: "Zapisano ustawienia oceny." });
    } catch (error) {
      setState({
        saving: false,
        message: apiErrorMessage(error, "Nie udało się zapisać ustawień."),
      });
    }
  }

  return (
    <form
      className="grid gap-3 sm:grid-cols-2"
      onSubmit={(event) => {
        event.preventDefault();
        void save();
      }}
    >
      <label htmlFor={ids.evaluators} className="flex flex-col gap-1 text-sm">
        Liczba ekspertów oceniających jeden wniosek
        <input
          id={ids.evaluators}
          type="number"
          min={1}
          max={20}
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={evaluators}
          onChange={(event) => setEvaluators(event.target.value)}
        />
      </label>

      <label htmlFor={ids.aggregation} className="flex flex-col gap-1 text-sm">
        Wynik wniosku z kart ekspertów
        <select
          id={ids.aggregation}
          className="w-48 rounded-sm border border-border-control px-2 py-1"
          value={aggregation}
          onChange={(event) =>
            setAggregation(
              event.target
                .value as EvaluationSettingsRequest["scoreAggregation"],
            )
          }
        >
          <option value="Sum">suma punktów</option>
          <option value="Average">średnia punktów</option>
        </select>
      </label>

      <label htmlFor={ids.threshold} className="flex flex-col gap-1 text-sm">
        Próg punktowy (puste: bez progu)
        <input
          id={ids.threshold}
          type="number"
          min={0}
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={threshold}
          onChange={(event) => setThreshold(event.target.value)}
        />
      </label>

      <label htmlFor={ids.divergence} className="flex flex-col gap-1 text-sm">
        Próg rozbieżności ocen w procentach skali (puste: bez ostrzeżenia)
        <input
          id={ids.divergence}
          type="number"
          min={0}
          max={100}
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={divergence}
          onChange={(event) => setDivergence(event.target.value)}
        />
      </label>

      <label className="flex items-center gap-2 text-sm sm:col-span-2">
        <input
          type="checkbox"
          checked={includesStrategic}
          onChange={(event) => setIncludesStrategic(event.target.checked)}
        />
        Punkty za kryteria strategiczne liczą się do progu
      </label>

      <div className="flex items-center gap-3 sm:col-span-2">
        <button
          type="submit"
          disabled={state.saving}
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
        >
          Zapisz ustawienia
        </button>
        {state.message !== null ? (
          <p role="status" className="text-sm">
            {state.message}
          </p>
        ) : null}
      </div>
    </form>
  );
}
