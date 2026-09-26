"use client";

import { useEffect, useRef, useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { ApiError, apiErrorMessage } from "@/lib/api-client";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { reportFormOf, saveReport, submitReport, type Report } from "@/lib/reports";

/** A second of quiet before the report saves, the pace of every other draft. */
export const REPORT_AUTOSAVE_DELAY_MS = 1000;

type SaveState = "idle" | "saving" | "saved" | "failed";

/**
 * The applicant filling in a report (T-50a): the same renderer as the
 * application, values from the application shown as text ("było") next to
 * the fields to fill in ("jest"), autosave, and submission only through a
 * confirmation, because a submitted report is closed until the operator
 * sends it back.
 */
export function ReportWorkspace({ report: initial, onSubmitted }: { report: Report; onSubmitted: (report: Report) => void }) {
  const [report, setReport] = useState(initial);
  const [saveState, setSaveState] = useState<SaveState>("idle");
  const [confirming, setConfirming] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [problems, setProblems] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const pending = useRef<FormAnswers | null>(null);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const form = reportFormOf(report);

  useEffect(
    () => () => {
      if (timer.current) clearTimeout(timer.current);
    },
    [],
  );

  async function flush(): Promise<boolean> {
    if (timer.current) {
      clearTimeout(timer.current);
      timer.current = null;
    }
    const answers = pending.current;
    if (answers === null) return true;
    pending.current = null;

    setSaveState("saving");
    try {
      setReport(await saveReport(report.id, answers));
      setSaveState("saved");
      return true;
    } catch {
      pending.current = answers;
      setSaveState("failed");
      return false;
    }
  }

  function onChange(answers: FormAnswers) {
    pending.current = answers;
    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => void flush(), REPORT_AUTOSAVE_DELAY_MS);
  }

  async function submit() {
    setSubmitting(true);
    setError(null);
    try {
      if (!(await flush())) {
        setError("Nie udało się zapisać ostatnich zmian. Spróbuj jeszcze raz.");
        return;
      }
      const submitted = await submitReport(report.id);
      setConfirming(false);
      setProblems([]);
      onSubmitted(submitted);
    } catch (failure) {
      if (failure instanceof ApiError && Object.keys(failure.fieldErrors).length > 0) {
        setProblems(Object.values(failure.fieldErrors).flat());
        setConfirming(false);
      } else {
        setError(apiErrorMessage(failure, "Nie udało się złożyć sprawozdania."));
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <section aria-labelledby="sprawozdanie" className="flex flex-col gap-4">
      <h2 id="sprawozdanie" className="text-xl">
        Sprawozdanie
      </h2>
      <p className="text-sm">
        Wartości z wniosku są pokazane jako tekst i nie da się ich zmienić. Obok wpisz, jak było naprawdę.
      </p>

      <FormRenderer
        document={form.document}
        initialAnswers={form.answers}
        competitionSettings={{}}
        applicant={form.applicant}
        onChange={onChange}
      />

      {problems.length > 0 ? (
        <div role="alert" className="flex flex-col gap-1 text-sm">
          <p>Sprawozdania nie da się jeszcze złożyć:</p>
          <ul className="list-disc pl-6">
            {problems.map((problem, index) => (
              <li key={index}>{problem}</li>
            ))}
          </ul>
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-4">
        <p className="text-sm" aria-live="polite">
          {saveState === "saving" ? "Zapisywanie…" : null}
          {saveState === "saved" ? "Zapisano." : null}
          {saveState === "failed" ? "Nie udało się zapisać. Zmiany zostaną wysłane przy następnej próbie." : null}
        </p>
        <button
          type="button"
          className="ml-auto rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover"
          onClick={() => setConfirming(true)}
        >
          Złóż sprawozdanie
        </button>
      </div>

      {confirming ? (
        <ConfirmDialog
          title="Po złożeniu sprawozdania nie zmienisz go, chyba że operator zwróci je do poprawy."
          confirmLabel="Złóż sprawozdanie"
          busyLabel="Składanie…"
          busy={submitting}
          error={error}
          onCancel={() => {
            setConfirming(false);
            setError(null);
          }}
          onConfirm={() => void submit()}
        />
      ) : null}
    </section>
  );
}
