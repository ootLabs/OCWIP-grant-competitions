"use client";

import { useCallback, useEffect, useState } from "react";

import { ApiError } from "@/lib/api-client";
import { newDraft } from "@/lib/competition-wizard/draft-factory";
import {
  clearWizardDraft,
  loadWizardDraft,
  saveWizardDraft,
} from "@/lib/competition-wizard/draft-storage";
import { stepsForFields } from "@/lib/competition-wizard/field-steps";
import { toCompetitionRequest } from "@/lib/competition-wizard/to-request";
import type { CompetitionDraft, WizardStepId } from "@/lib/competition-wizard/types";
import {
  createCompetition,
  fetchOperators,
  publishCompetition,
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
const GENERIC_PUBLISH_ERROR =
  "Nie udało się opublikować konkursu. Spróbuj ponownie.";

/**
 * Kreator ogłoszenia konkursu (T-22): siedem kroków plus podsumowanie z
 * publikacją. Krok 0 (kopia z poprzedniego roku) świadomie nie tu, karta go
 * nie wymienia i wymaga kart oceny i wzorów dokumentów, których jeszcze nie
 * ma (R-11 w docs/runbook/rozbieznosci.md). Edycja istniejącego konkursu z
 * ostrzeżeniem o wpłyniętych wnioskach też nie tu: wnioski jeszcze nie
 * istnieją (T-29, T-33 w kolejce), więc ostrzeżenie nie miałoby czego pokazać.
 */
export default function CompetitionWizardPage() {
  const [draft, setDraft] = useState<CompetitionDraft>(newDraft());
  const [competitionId, setCompetitionId] = useState<string | null>(null);
  const [savedAt, setSavedAt] = useState<string | null>(null);
  const [saved, setSaved] = useState<OperatorCompetition | null>(null);
  const [currentStep, setCurrentStep] = useState<WizardStepId>("basics");
  const [saving, setSaving] = useState(false);
  const [structuralGaps, setStructuralGaps] = useState<readonly string[]>([]);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [operators, setOperators] = useState<OperatorAccount[] | null>(null);
  const [publishing, setPublishing] = useState(false);
  const [publishError, setPublishError] = useState<string | null>(null);
  const [published, setPublished] = useState(false);

  useEffect(() => {
    const stored = loadWizardDraft();
    if (stored !== null) {
      setDraft(stored.draft);
      setCompetitionId(stored.competitionId);
      setSavedAt(stored.savedAt);
    }
  }, []);

  useEffect(() => {
    let current = true;
    fetchOperators()
      .then((list) => {
        if (current) {
          setOperators(list);
        }
      })
      .catch(() => {
        if (current) {
          // An empty list degrades to "no contact selectable yet" rather than
          // blocking the rest of the wizard.
          setOperators([]);
        }
      });
    return () => {
      current = false;
    };
  }, []);

  const updateDraft = useCallback(
    (patch: Partial<CompetitionDraft>) => {
      setDraft((previous) => {
        const next = { ...previous, ...patch };
        saveWizardDraft(next, competitionId);
        return next;
      });
    },
    [competitionId],
  );

  /** Null on failure (a message is already in state for the caller to show). */
  const attemptSave = useCallback(async (): Promise<OperatorCompetition | null> => {
    const { request, structuralGaps: gaps } = toCompetitionRequest(draft);

    if (request === null) {
      setStructuralGaps(gaps);
      return null;
    }

    setStructuralGaps([]);
    setSaving(true);
    setSaveError(null);

    try {
      const response =
        competitionId === null
          ? await createCompetition(request)
          : await updateCompetition(competitionId, request);

      setCompetitionId(response.id);
      setSaved(response);
      setSavedAt(new Date().toISOString());
      saveWizardDraft(draft, response.id);
      setFieldErrors({});
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
  }, [draft, competitionId]);

  // Entering the summary step refreshes the preview from what the backend
  // actually has, because "dokładnie to, co zobaczy wnioskodawca" has to come
  // from a saved competition, not from the draft sitting in the browser.
  useEffect(() => {
    if (currentStep === "summary" && !published) {
      void attemptSave();
    }
    // Only on ENTERING the step, not on every keystroke that also changes
    // attemptSave's identity: a fresh save on every character would fight
    // the operator for the network.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [currentStep]);

  const publish = useCallback(async () => {
    setPublishError(null);
    // Saves (create or update) first, and publishes the id THAT call handed
    // back, not competitionId from the surrounding closure: on the very
    // first save those are the same value, but only after this await has
    // resolved.
    const justSaved = await attemptSave();
    if (justSaved === null) {
      return;
    }

    setPublishing(true);
    try {
      const response = await publishCompetition(justSaved.id);
      setSaved(response);
      setPublished(true);
      clearWizardDraft();
    } catch (error) {
      if (error instanceof ApiError && error.detail !== null) {
        setPublishError(error.detail);
      } else {
        setPublishError(GENERIC_PUBLISH_ERROR);
      }
    } finally {
      setPublishing(false);
    }
  }, [attemptSave]);

  const stepsWithErrors = new Set(stepsForFields(Object.keys(fieldErrors)));

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl">Kreator ogłoszenia konkursu</h1>

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
          <WizardNav
            current={currentStep}
            stepsWithErrors={stepsWithErrors}
            onSelect={setCurrentStep}
          />
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
            <StepDescription
              draft={draft}
              onChange={updateDraft}
              fieldErrors={fieldErrors}
            />
          ) : null}

          {currentStep === "paper" ? (
            <StepPaper
              draft={draft}
              onChange={updateDraft}
              fieldErrors={fieldErrors}
            />
          ) : null}

          {currentStep === "limits" ? (
            <StepLimits
              draft={draft}
              onChange={updateDraft}
              fieldErrors={fieldErrors}
            />
          ) : null}

          {currentStep === "attachments" ? (
            <StepAttachments
              draft={draft}
              onChange={updateDraft}
              fieldErrors={fieldErrors}
            />
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
              onPublish={() => void publish()}
              publishing={publishing}
              publishError={publishError}
              published={published}
            />
          ) : null}
        </div>
      </div>
    </div>
  );
}
