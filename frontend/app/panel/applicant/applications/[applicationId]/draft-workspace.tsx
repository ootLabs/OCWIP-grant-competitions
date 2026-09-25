"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { IntakeCountdown } from "@/app/competitions/intake-countdown";
import { OfferView } from "@/components/offer-view";
import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { ApiError } from "@/lib/api-client";
import {
  limitSettingsFrom,
  saveDraft,
  submitApplication,
  type Application,
  type ApplicationForm,
  type Attachment,
} from "@/lib/applicant-applications";
import type { PublicCompetition } from "@/lib/competitions";
import { formatTimeOnly } from "@/lib/format";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { submissionGaps, type SubmissionGap } from "@/lib/forms/submission-gaps";

import { AttachmentsPanel, attachmentsAnchorId } from "./attachments-panel";
import { ConfirmSubmitDialog } from "./confirm-submit-dialog";
import { TechnicalBlock } from "./technical-block";

/** Not on every keystroke (a five minute autosave the card refuses), and not
 * on every keystroke either: "po każdym wypełnionym polu" is a pause in
 * typing, not a character. One second of quiet is that pause. */
const AUTOSAVE_DELAY_MS = 1000;

type Stage = "filling" | "reviewing";

/**
 * Kroki 3.2 to 3.7 of proces.md, in one component: filling the form with
 * autosave, the attachments, the always visible "Złóż wniosek" with its list
 * of what is missing, the read only summary with "popraw" per section, and
 * the one confirmation the submission itself goes through.
 */
export function DraftWorkspace({
  application: initialApplication,
  form,
  competition,
  initialAttachments,
  onSubmitted,
}: {
  application: Application;
  form: ApplicationForm;
  competition: PublicCompetition;
  initialAttachments: readonly Attachment[];
  onSubmitted: (application: Application) => void;
}) {
  // The whole row, not only its answers: the checksum (D15) changes with
  // every save, and TechnicalBlock below has to show the one that matches
  // what was actually last written, not the one from the initial GET.
  const [application, setApplication] = useState(initialApplication);
  const [answers, setAnswers] = useState<FormAnswers>(initialApplication.answers as FormAnswers);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [attachments, setAttachments] = useState<Attachment[]>([...initialAttachments]);
  const [stage, setStage] = useState<Stage>("filling");
  const [activeSectionKey, setActiveSectionKey] = useState(
    form.document.sections[0]?.key ?? "",
  );
  const [focusTarget, setFocusTarget] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const competitionSettings = useMemo(() => limitSettingsFrom(competition), [competition]);
  const fieldGaps = useMemo(
    () => submissionGaps(form.document, answers, competitionSettings),
    [form.document, answers, competitionSettings],
  );

  // Coarse, and deliberately so: R-33, an uploaded file carries no link to
  // which requirement it answers, so this can only ever say "the competition
  // asks for something and nothing at all has been added", never "the right
  // thing is missing". A precise check would be a lie the backend does not
  // back up either (T-33's own submission check skips it for the same
  // reason).
  const needsAnyAttachment =
    competition.attachments.some((item) => item.requirement !== "Optional") &&
    attachments.length === 0;

  const gaps: SubmissionGap[] = needsAnyAttachment
    ? [
        ...fieldGaps,
        {
          sectionKey: "",
          sectionTitle: "",
          fieldKey: "zalaczniki",
          fieldLabel: "Załączniki",
          message: "Ten konkurs wymaga załączników, a nie dodano żadnego pliku.",
          anchorId: attachmentsAnchorId,
        },
      ]
    : fieldGaps;

  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (saveTimer.current !== null) {
        clearTimeout(saveTimer.current);
      }
    },
    [],
  );

  const onChange = useCallback(
    (next: FormAnswers) => {
      setAnswers(next);

      if (saveTimer.current !== null) {
        clearTimeout(saveTimer.current);
      }

      saveTimer.current = setTimeout(() => {
        setSaving(true);
        saveDraft(application.id, next)
          .then((updated) => {
            setApplication(updated);
            setSaveError(null);
          })
          .catch((error: unknown) => {
            setSaveError(
              error instanceof ApiError && error.detail !== null
                ? error.detail
                : "Nie udało się zapisać. Sprawdź połączenie: odpowiedzi zostają w formularzu.",
            );
          })
          .finally(() => setSaving(false));
      }, AUTOSAVE_DELAY_MS);
    },
    [application.id],
  );

  useEffect(() => {
    if (focusTarget === null) {
      return;
    }
    const target = document.getElementById(focusTarget);
    // Optional even on the method itself: jsdom (this component's own
    // tests) has no scrollIntoView at all, and calling it unconditionally
    // would throw before the focus below, the part a keyboard user needs.
    target?.scrollIntoView?.({ block: "center" });
    // The wrapper itself (field-view.tsx, table-field.tsx) is not a control
    // a browser will focus: the actual input inside it is what a keyboard
    // user needs to land on.
    target?.querySelector<HTMLElement>("input, textarea, select, button")?.focus({
      preventScroll: true,
    });
    setFocusTarget(null);
    // Re-runs once the section the target lives on has actually mounted.
  }, [focusTarget, activeSectionKey]);

  const jumpToGap = useCallback((gap: SubmissionGap) => {
    setStage("filling");
    // An empty sectionKey is the attachments gap, not a form section: it has
    // nothing to set FormRenderer's active section to, and setting one that
    // does not exist would blank the form out entirely.
    if (gap.sectionKey !== "") {
      setActiveSectionKey(gap.sectionKey);
    }
    setFocusTarget(gap.anchorId);
  }, []);

  /** "Popraw" on the summary screen: the section only, no specific field to
   * focus, unlike a gap which always names one. */
  const goToSection = useCallback((sectionKey: string) => {
    setStage("filling");
    setActiveSectionKey(sectionKey);
  }, []);

  async function handleConfirmedSubmit() {
    setSubmitting(true);
    setSubmitError(null);

    try {
      onSubmitted(await submitApplication(application.id));
    } catch (error) {
      setSubmitError(
        error instanceof ApiError && error.detail !== null
          ? error.detail
          : "Nie udało się złożyć wniosku.",
      );
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm">
        {saving
          ? "Zapisywanie…"
          : saveError !== null
            ? saveError
            : `Zapisano o ${formatTimeOnly(application.lastSavedAt)}`}
      </p>

      <IntakeCountdown
        closesAt={competition.intake.acceptsApplications ? competition.intake.closesAt : null}
        message={competition.intake.message}
      />

      <SubmitBar
        stage={stage}
        gaps={gaps}
        onJump={jumpToGap}
        onContinue={() => (stage === "filling" ? setStage("reviewing") : setConfirmOpen(true))}
      />

      {stage === "filling" ? (
        <>
          <FormRenderer
            document={form.document}
            initialAnswers={answers}
            competitionSettings={competitionSettings}
            onChange={onChange}
            activeSectionKey={activeSectionKey}
            onActiveSectionChange={setActiveSectionKey}
          />

          <AttachmentsPanel
            applicationId={application.id}
            requirements={competition.attachments}
            attachments={attachments}
            onUploaded={(attachment) => setAttachments((previous) => [...previous, attachment])}
            onReplaced={(replacedId, attachment) =>
              setAttachments((previous) => [
                ...previous.filter((existing) => existing.id !== replacedId),
                attachment,
              ])
            }
          />
        </>
      ) : (
        <>
          <h2 className="text-xl">Podsumowanie wniosku</h2>
          <OfferView document={form.document} answers={answers} onEditSection={goToSection} />
        </>
      )}

      <TechnicalBlock application={application} versionNumber={form.versionNumber} />

      {confirmOpen ? (
        <ConfirmSubmitDialog
          submitting={submitting}
          error={submitError}
          onCancel={() => {
            setConfirmOpen(false);
            setSubmitError(null);
          }}
          onConfirm={handleConfirmedSubmit}
        />
      ) : null}
    </div>
  );
}

function SubmitBar({
  stage,
  gaps,
  onJump,
  onContinue,
}: {
  stage: Stage;
  gaps: readonly SubmissionGap[];
  onJump: (gap: SubmissionGap) => void;
  onContinue: () => void;
}) {
  const ready = gaps.length === 0;

  return (
    <div className="flex flex-col gap-2 rounded-sm border border-border-muted bg-surface-muted px-4 py-3 text-sm">
      <div className="flex items-center justify-between gap-3">
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover disabled:cursor-not-allowed disabled:opacity-40"
          disabled={!ready}
          onClick={onContinue}
        >
          Złóż wniosek
        </button>
      </div>

      {!ready ? (
        <div>
          <p>Zanim złożysz wniosek, uzupełnij:</p>
          <ul className="mt-1 flex list-none flex-col gap-1">
            {gaps.map((gap, index) => (
              <li key={`${gap.fieldKey}-${index}`}>
                <button
                  type="button"
                  className="text-left underline"
                  onClick={() => onJump(gap)}
                >
                  {gap.sectionTitle ? `${gap.sectionTitle}: ` : ""}
                  {gap.fieldLabel} - {gap.message}
                </button>
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </div>
  );
}
