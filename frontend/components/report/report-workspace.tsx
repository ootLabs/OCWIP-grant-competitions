"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { useFieldFocus } from "@/components/form-renderer/use-field-focus";
import { ApiError, apiErrorMessage } from "@/lib/api-client";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { submissionGaps, type SubmissionGap } from "@/lib/forms/submission-gaps";
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
 *
 * The button waits for a complete report, with the list of what is missing
 * under it and every item jumping to its field, exactly as the application's
 * SubmitBar does (B-GUI-18): it used to be clickable with a section already
 * marked "(są błędy)", so the refusal arrived after a confirmation dialog,
 * two clicks later. The server still has the last word and what it refuses
 * is listed below: these are the same rules, run early, not instead.
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

  // What is on screen right now, which is what the gaps below are about; the
  // saved copy lives in `report` and may lag behind until the next autosave.
  const [answers, setAnswers] = useState<FormAnswers>(form.answers);
  const [activeSectionKey, setActiveSectionKey] = useState(form.document.sections[0]?.key ?? "");
  const [focusTarget, setFocusTarget] = useState<string | null>(null);
  const clearFocusTarget = useCallback(() => setFocusTarget(null), []);
  useFieldFocus(focusTarget, activeSectionKey, clearFocusTarget);

  // A report measures no limit against the competition's settings; the
  // application did, and the server says the same (ReportService.Bases).
  const gaps = useMemo(() => submissionGaps(form.document, answers, {}), [form.document, answers]);

  const jumpToGap = useCallback((gap: SubmissionGap) => {
    setActiveSectionKey(gap.sectionKey);
    setFocusTarget(gap.anchorId);
  }, []);

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
    setAnswers(answers);
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
        activeSectionKey={activeSectionKey}
        onActiveSectionChange={setActiveSectionKey}
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
          className="ml-auto rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
          disabled={gaps.length > 0}
          onClick={() => setConfirming(true)}
        >
          Złóż sprawozdanie
        </button>
      </div>

      {gaps.length > 0 ? (
        <div className="flex flex-col gap-2 rounded-sm border border-border-muted bg-surface-muted px-4 py-3 text-sm">
          <p>Zanim złożysz sprawozdanie, uzupełnij:</p>
          <ul className="flex list-none flex-col gap-1">
            {gaps.map((gap, index) => (
              <li key={`${gap.fieldKey}-${index}`}>
                <button type="button" className="text-left underline" onClick={() => jumpToGap(gap)}>
                  {gap.sectionTitle ? `${gap.sectionTitle}: ` : ""}
                  {gap.fieldLabel} - {gap.message}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}

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
