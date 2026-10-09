"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState } from "react";

import { ApiError } from "@/lib/api-client";
import {
  clearWizardDraft,
  loadWizardDraft,
  saveWizardDraft,
} from "@/lib/competition-wizard/draft-storage";
import { stepsForFields } from "@/lib/competition-wizard/field-steps";
import { toCompetitionRequest } from "@/lib/competition-wizard/to-request";
import { WIZARD_STEPS, type CompetitionDraft, type WizardStepId } from "@/lib/competition-wizard/types";
import {
  createCompetition,
  fetchOperators,
  updateCompetition,
  type OperatorAccount,
  type OperatorCompetition,
} from "@/lib/operator-competitions";

import { SaveBar } from "./save-bar";
import { StepAttachments } from "./step-attachments";
import { StepBasics } from "./step-basics";
import { StepContacts } from "./step-contacts";
import { StepDescription } from "./step-description";
import { StepLimits } from "./step-limits";
import { StepPaper } from "./step-paper";
import { StepSummary } from "./step-summary";
import { WizardNav } from "./wizard-nav";

const GENERIC_SAVE_ERROR = "Nie udało się zapisać konkursu. Spróbuj ponownie.";

/**
 * Two timestamps as instants, not as text: the answer to a save carries
 * .NET's seven fractional digits and a later read from PostgreSQL six, so
 * the same version would otherwise never compare equal.
 */
function sameInstant(left: string | null, right: string): boolean {
  return left !== null && new Date(left).getTime() === new Date(right).getTime();
}

/** The step named in ?krok=, when it is one this wizard knows. */
export function stepFromQuery(value: string | null): WizardStepId | undefined {
  return WIZARD_STEPS.find((step) => step === value);
}

/** Where a saved competition is edited: the address a reload comes back to. */
export function editPath(id: string): string {
  return `/panel/operator/competitions/${id}/edit`;
}

/**
 * Kreator ogłoszenia konkursu (T-22): siedem kroków plus podsumowanie. Since
 * T-97 the same wizard edits a saved competition, opened from the server, so
 * a draft started in one browser continues in another; `localStorage` only
 * buffers what was typed since the last save (draft-storage.ts). Publication
 * moved to the competition page, because it needs the form and both
 * evaluation cards, which only a saved competition can have.
 */
export function CompetitionWizard({
  initialDraft,
  initialCompetition,
  initialStep,
  submittedApplications = 0,
}: {
  initialDraft: CompetitionDraft;
  /** Null for a competition not saved yet. */
  initialCompetition: OperatorCompetition | null;
  /** Step to open on, carried across the move to the saved address. */
  initialStep?: WizardStepId;
  /** Applications already received, for the warning above the steps. */
  submittedApplications?: number;
}) {
  const [draft, setDraft] = useState<CompetitionDraft>(initialDraft);
  const [saved, setSaved] = useState<OperatorCompetition | null>(initialCompetition);
  const [savedAt, setSavedAt] = useState<string | null>(initialCompetition?.updatedAt ?? null);
  const [restored, setRestored] = useState(false);
  const [currentStep, setCurrentStep] = useState<WizardStepId>(initialStep ?? "basics");
  const [saving, setSaving] = useState(false);
  // Whether a save has been refused for missing fields. The list itself is
  // read live from the draft below, so a gap filled in disappears at once
  // instead of hanging there until the next save (P4-07).
  const [gapsShown, setGapsShown] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [operators, setOperators] = useState<OperatorAccount[] | null>(null);
  const router = useRouter();

  const competitionId = saved?.id ?? null;

  useEffect(() => {
    const stored = loadWizardDraft(initialCompetition?.id ?? null);
    if (stored === null) {
      return;
    }

    // A buffer typed on top of an older version would undo whatever was
    // saved since, so it is only offered back on the version it came from.
    if (initialCompetition === null || sameInstant(stored.baseUpdatedAt, initialCompetition.updatedAt)) {
      setDraft(stored.draft);
      setSavedAt(stored.savedAt);
      setRestored(initialCompetition !== null);
    } else {
      clearWizardDraft(initialCompetition.id);
    }
  }, [initialCompetition]);

  useEffect(() => {
    let current = true;
    fetchOperators()
      .then((list) => current && setOperators(list))
      // An empty list degrades to "no contact selectable yet" rather than
      // blocking the rest of the wizard.
      .catch(() => current && setOperators([]));
    return () => {
      current = false;
    };
  }, []);

  const updateDraft = useCallback(
    (patch: Partial<CompetitionDraft>) => {
      setDraft((previous) => {
        const next = { ...previous, ...patch };
        saveWizardDraft(next, competitionId, saved?.updatedAt ?? null);
        return next;
      });
    },
    [competitionId, saved],
  );

  const discardRestored = useCallback(() => {
    if (initialCompetition !== null) {
      clearWizardDraft(initialCompetition.id);
      setDraft(initialDraft);
      setSavedAt(initialCompetition.updatedAt);
    }
    setRestored(false);
  }, [initialCompetition, initialDraft]);

  /** Null on failure (a message is already in state for the caller to show). */
  const attemptSave = useCallback(async (): Promise<OperatorCompetition | null> => {
    const { request, structuralGaps: gaps } = toCompetitionRequest(draft);

    if (request === null) {
      setGapsShown(gaps.length > 0);
      return null;
    }

    setGapsShown(false);
    setSaving(true);
    setSaveError(null);

    try {
      const response =
        competitionId === null
          ? await createCompetition(request)
          : await updateCompetition(competitionId, request);

      setSaved(response);
      setSavedAt(new Date().toISOString());
      setFieldErrors({});
      setRestored(false);
      // Everything typed is on the server now: no buffer left to keep.
      clearWizardDraft(response.id);

      if (competitionId === null) {
        clearWizardDraft(null);
        // A real navigation, not window.history.replaceState. The bare history
        // call moved the address but left this component mounted under a route
        // that no longer matched it, and the remount that followed took the
        // typed draft with it: the operator saw an empty wizard saying
        // "Konkurs jeszcze nie zapisany" over a competition that was already
        // in the database, and filling it in again hit the unique number.
        // The edit route reads the competition back from the server, which is
        // the single source of it from here on; the step rides along so
        // nobody is thrown back to 1.1.
        router.replace(`${editPath(response.id)}?krok=${currentStep}`);
      }

      return response;
    } catch (error) {
      if (error instanceof ApiError && error.status === 400) {
        setFieldErrors(error.fieldErrors);
        setSaveError("Konkurs nie przeszedł sprawdzenia. Popraw pola niżej.");
      } else if (error instanceof ApiError && error.detail !== null) {
        setSaveError(error.detail);
      } else {
        setSaveError(GENERIC_SAVE_ERROR);
      }
      return null;
    } finally {
      setSaving(false);
    }
  }, [draft, competitionId, currentStep, router]);

  // Entering the summary step refreshes the preview from what the backend
  // actually has, because "dokładnie to, co zobaczy wnioskodawca" has to come
  // from a saved competition, not from the draft sitting in the browser.
  useEffect(() => {
    if (currentStep === "summary") {
      void attemptSave();
    }
    // Only on ENTERING the step, not on every keystroke that also changes
    // attemptSave's identity: a fresh save on every character would fight
    // the operator for the network.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentStep]);

  const liveConversion = toCompetitionRequest(draft);
  const structuralGaps = gapsShown ? liveConversion.structuralGaps : [];
  // A gap found before sending marks its step the same way a backend field
  // error does: "krok dostaje oznaczenie" whichever side noticed it.
  const stepsWithErrors = new Set(
    stepsForFields([
      ...Object.keys(fieldErrors),
      ...(gapsShown ? liveConversion.structuralGapFields : []),
    ]),
  );

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl">
        {initialCompetition === null ? "Kreator ogłoszenia konkursu" : `Edycja konkursu ${initialCompetition.number}`}
      </h1>

      {submittedApplications > 0 ? (
        <p role="note" className="rounded-sm border border-brand-accent px-3 py-2 text-sm">
          Do tego konkursu wpłynęło już wniosków: {submittedApplications}. Zmiana terminów, kwot
          albo wymaganych załączników dotyczy wnioskodawców, którzy już złożyli wniosek według
          obecnych zasad.
        </p>
      ) : null}

      {restored ? (
        <p role="status" className="text-sm">
          Przywrócono zmiany wpisane w tej przeglądarce, których jeszcze nie zapisano.{" "}
          <button type="button" className="underline" onClick={discardRestored}>
            Odrzuć je
          </button>
        </p>
      ) : null}

      <SaveBar
        savedAt={savedAt}
        saving={saving}
        structuralGaps={structuralGaps}
        errorMessage={saveError}
        stepsWithErrors={stepsWithErrors}
        onSave={() => void attemptSave()}
      />

      <div className="flex flex-col gap-6 md:flex-row">
        <div className="md:w-64 md:shrink-0">
          <WizardNav current={currentStep} stepsWithErrors={stepsWithErrors} onSelect={setCurrentStep} />
        </div>

        <div className="flex-1">
          {currentStep === "basics" ? (
            <StepBasics
              draft={draft}
              onChange={updateDraft}
              fieldErrors={fieldErrors}
              competitionId={competitionId}
            />
          ) : null}

          {currentStep === "description" ? (
            <StepDescription draft={draft} onChange={updateDraft} fieldErrors={fieldErrors} />
          ) : null}

          {currentStep === "paper" ? (
            <StepPaper draft={draft} onChange={updateDraft} fieldErrors={fieldErrors} />
          ) : null}

          {currentStep === "limits" ? (
            <StepLimits draft={draft} onChange={updateDraft} fieldErrors={fieldErrors} />
          ) : null}

          {currentStep === "attachments" ? (
            <StepAttachments draft={draft} onChange={updateDraft} fieldErrors={fieldErrors} />
          ) : null}

          {currentStep === "contacts" ? (
            <StepContacts
              draft={draft}
              onChange={updateDraft}
              fieldErrors={fieldErrors}
              operators={operators}
            />
          ) : null}

          {currentStep === "summary" ? (
            <StepSummary
              saved={saved}
              saving={saving}
              structuralGaps={structuralGaps}
              saveError={saveError}
              onNavigate={setCurrentStep}
              onRetrySave={() => void attemptSave()}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}
