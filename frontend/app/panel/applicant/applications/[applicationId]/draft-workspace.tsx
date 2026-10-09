"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import { IntakeCountdown } from "@/app/competitions/intake-countdown";
import { OfferView } from "@/components/offer-view";
import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { useFieldFocus } from "@/components/form-renderer/use-field-focus";
import { ApiError, apiErrorMessage } from "@/lib/api-client";
import { lockOutside, type ApplicationReturn } from "@/lib/application-corrections";
import {
  limitSettingsFrom,
  saveDraft,
  submitApplication,
  type Application,
  type ApplicationForm,
  type Attachment,
} from "@/lib/applicant-applications";
import type { PublicCompetition } from "@/lib/competitions";
import { formatMoment, formatTimeOnly } from "@/lib/format";
import type { FormAnswers } from "@/lib/forms/answer-types";
import { applicantKindGap, prefilledApplicantKind } from "@/lib/forms/applicant-kind";
import type { ApplicantKind } from "@/lib/forms/document-types";
import { submissionGaps, type SubmissionGap } from "@/lib/forms/submission-gaps";
import { withReturnUrl } from "@/lib/login";
import { loginPath } from "@/lib/session";

import { AttachmentsPanel, requirementAnchorId } from "./attachments-panel";
import { ConfirmSubmitDialog } from "./confirm-submit-dialog";
import { type Stage, SubmitBar } from "./submit-bar";
import { TechnicalBlock } from "./technical-block";

/** Not on every keystroke (a five minute autosave the card refuses), and not
 * on every keystroke either: "po każdym wypełnionym polu" is a pause in
 * typing, not a character. One second of quiet is that pause. */
const AUTOSAVE_DELAY_MS = 1000;

/** How soon the page asks again when the server still accepted a save past this browser's deadline (O-19). */
const SERVER_CHECK_RETRY_MS = 30_000;

/** Where the applicant last was, per application, so coming back opens there. */
const sectionStorageKey = (applicationId: string) => `ocwip.application-section.${applicationId}`;

function readStoredSection(applicationId: string): string | null {
  try {
    return window.localStorage.getItem(sectionStorageKey(applicationId));
  } catch {
    return null;
  }
}

function storeSection(applicationId: string, sectionKey: string): void {
  try {
    window.localStorage.setItem(sectionStorageKey(applicationId), sectionKey);
  } catch {
    // A private window or blocked storage: the next visit opens at part I.
  }
}

/**
 * The beginnings of the refusals that a retry cannot change: the intake or
 * the correction window closed (CompetitionIntakeMessage on the server). A
 * 409 also answers "changed meanwhile" and "applicant data incomplete",
 * which ask for another try, so the status alone does not decide (O-19).
 */
const FINAL_REFUSALS = ["Nabór został zamknięty", "Termin poprawy minął", "Ten konkurs nie przyjmuje wniosków"];

function isFinalRefusal(error: unknown, message: string): boolean {
  return error instanceof ApiError && error.status === 409 && FINAL_REFUSALS.some((start) => message.startsWith(start));
}

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
  onAttachmentsChange,
  correction = null,
  cardType = null,
}: {
  application: Application;
  form: ApplicationForm;
  competition: PublicCompetition;
  initialAttachments: readonly Attachment[];
  onSubmitted: (application: Application) => void;
  /** So the page above keeps a fresh copy: it hands SubmittedView whatever
   * was uploaded here once the submit that follows succeeds. */
  onAttachmentsChange: (attachments: Attachment[]) => void;
  /** An open return (T-103): only its sections are editable, until its deadline. */
  correction?: ApplicationReturn | null;
  /** The type of the card the draft is filed for (O-10), null when unknown. */
  cardType?: ApplicantKind | null;
}) {
  // Outside the unlocked sections every field is shown, never an input; the
  // server refuses a change there all the same (LockedSections).
  const shownDocument = useMemo(
    () => (correction ? lockOutside(form.document, correction.sections) : form.document),
    [correction, form.document],
  );

  // The whole row, not only its answers: the checksum (D15) changes with
  // every save, and TechnicalBlock below has to show the one that matches
  // what was actually last written, not the one from the initial GET.
  const [application, setApplication] = useState(initialApplication);
  // O-10: a group without a patron can apply as nothing else, so the kind
  // of applicant starts filled in, before the renderer reads its answers.
  const [prefilled] = useState(() =>
    correction ? null : prefilledApplicantKind(form.document, initialApplication.answers as FormAnswers, cardType),
  );
  const [answers, setAnswers] = useState<FormAnswers>(prefilled ?? (initialApplication.answers as FormAnswers));
  const [saveError, setSaveError] = useState<string | null>(null);
  // The session ended under an open form (logged out on another device, P4-15).
  // Said in words of its own: the server's generic 401 sentence read "Zaloguj
  // się, żeby zobaczyć tę stronę" in place of "Zapisano o", over a form the
  // person was typing into, and the change was lost without a word.
  const [sessionLost, setSessionLost] = useState(false);
  const [saving, setSaving] = useState(false);
  const [attachments, setAttachments] = useState<Attachment[]>([...initialAttachments]);
  const [stage, setStage] = useState<Stage>("filling");
  const [activeSectionKey, setActiveSectionKey] = useState(
    correction?.sections[0] ?? form.document.sections[0]?.key ?? "",
  );

  // "Treść i miejsce mają wrócić" (P4-11): the answers come back from the
  // server, the section from this browser. Read after mounting, not in the
  // initial state, so the server render and the first client render agree.
  // A return opens at the first section to correct, as before.
  const applicationId = initialApplication.id;
  useEffect(() => {
    if (correction) {
      return;
    }
    const stored = readStoredSection(applicationId);
    if (stored !== null && form.document.sections.some((section) => section.key === stored)) {
      setActiveSectionKey(stored);
    }
  }, [applicationId, correction, form.document.sections]);

  // Stored on the applicant's own moves only, never from an effect: an effect
  // would write the first section on mounting, over the one about to be read.
  const selectSection = useCallback(
    (sectionKey: string) => {
      setActiveSectionKey(sectionKey);
      storeSection(applicationId, sectionKey);
    },
    [applicationId],
  );
  const [focusTarget, setFocusTarget] = useState<string | null>(null);
  const clearFocusTarget = useCallback(() => setFocusTarget(null), []);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  // The server refused for good (the intake or the correction window closed
  // while this page stayed open, O-19). The page said "Nabór trwa"
  // with an active button under the refusal; now it says what happened.
  const [closedMessage, setClosedMessage] = useState<string | null>(null);

  useEffect(() => {
    onAttachmentsChange(attachments);
  }, [attachments, onAttachmentsChange]);

  const competitionSettings = useMemo(() => limitSettingsFrom(competition), [competition]);
  const fieldGaps = useMemo(
    () => submissionGaps(shownDocument, answers, competitionSettings),
    [shownDocument, answers, competitionSettings],
  );

  // One gap per required attachment with no file answering it (T-101), the
  // same count the submission makes. "Required outside KRS" is left to the
  // server, which knows the register from the applicant's card.
  const gaps: SubmissionGap[] = useMemo(() => {
    const missing = competition.attachments.filter(
      (item) =>
        item.requirement === "Required" &&
        !attachments.some((attachment) => attachment.requirementId === item.id),
    );

    // A kind that does not fit the card, said now rather than at submission.
    const kindGap = applicantKindGap(shownDocument, answers, cardType);

    return [
      ...(kindGap ? [kindGap] : []),
      ...fieldGaps,
      ...missing.map((item) => ({
        sectionKey: "",
        sectionTitle: "",
        fieldKey: `zalacznik-${item.id}`,
        fieldLabel: item.title,
        message: `Brakuje wymaganego załącznika: ${item.title}.`,
        anchorId: requirementAnchorId(item.id),
      })),
    ];
  }, [fieldGaps, competition.attachments, attachments, shownDocument, answers, cardType]);

  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Every save gets the next number; only the response whose number still
  // matches the last one handed out is allowed to touch state. A slower,
  // now stale request finishing after a faster later one would otherwise
  // silently drag the shown checksum and "Zapisano o" back in time.
  const saveSeq = useRef(0);
  // The one in flight (or about to run) right now, so a submit can wait for
  // it instead of finalizing whatever the server still has from before it.
  const pendingSave = useRef<Promise<void> | null>(null);

  useEffect(
    () => () => {
      if (saveTimer.current !== null) {
        clearTimeout(saveTimer.current);
      }
    },
    [],
  );

  const performSave = useCallback(
    (next: FormAnswers): Promise<void> => {
      const seq = ++saveSeq.current;
      setSaving(true);

      const promise = saveDraft(application.id, next)
        .then((updated) => {
          if (seq === saveSeq.current) {
            setApplication(updated);
            setSaveError(null);
            setSessionLost(false);
          }
        })
        .catch((error: unknown) => {
          if (seq === saveSeq.current) {
            setSessionLost(error instanceof ApiError && error.status === 401);
            setSaveError(
              apiErrorMessage(
                error,
                "Nie udało się zapisać. Sprawdź połączenie: odpowiedzi zostają w formularzu.",
              ),
            );
          }
          throw error;
        })
        .finally(() => {
          if (seq === saveSeq.current) {
            setSaving(false);
          }
          if (pendingSave.current === promise) {
            pendingSave.current = null;
          }
        });

      pendingSave.current = promise;
      return promise;
    },
    [application.id],
  );

  const onChange = useCallback(
    (next: FormAnswers) => {
      setAnswers(next);

      if (saveTimer.current !== null) {
        clearTimeout(saveTimer.current);
      }

      saveTimer.current = setTimeout(() => {
        saveTimer.current = null;
        // The failure is already on screen (saveError, sessionLost); the
        // rethrow is for a submit waiting on this save, not for the timer.
        performSave(next).catch(() => {});
      }, AUTOSAVE_DELAY_MS);
    },
    [performSave],
  );

  // O-19: a page left open past the deadline went on saying "Nabór trwa"
  // with an active button. At the deadline by this browser's clock the page
  // asks the server, with an ordinary autosave of the same answers: the
  // server's refusal closes the page, an accepted save means this clock runs
  // ahead and the page asks again shortly. The browser clock alone would
  // shut a fast-clocked applicant out while the server still accepts.
  const answersRef = useRef(answers);
  answersRef.current = answers;
  const deadline = correction?.deadline ?? (competition.intake.acceptsApplications ? competition.intake.closesAt : null);
  useEffect(() => {
    if (deadline === null || deadline === undefined) {
      return;
    }
    let timer: ReturnType<typeof setTimeout> | undefined;
    let stopped = false;
    const arm = () => {
      const left = new Date(deadline).getTime() - Date.now();
      if (left > 0) {
        // setTimeout holds at most about 24 days; a longer wait is re-armed.
        timer = setTimeout(arm, Math.min(left, 2_000_000_000));
        return;
      }
      // Through a resolved promise, so a failure thrown before the request
      // starts lands in the same catch as a refusal.
      Promise.resolve()
        .then(() => performSave(answersRef.current))
        .then(() => {
          if (!stopped) {
            timer = setTimeout(arm, SERVER_CHECK_RETRY_MS);
          }
        })
        .catch((error: unknown) => {
          const message = apiErrorMessage(error, "");
          if (stopped) {
            return;
          }
          if (isFinalRefusal(error, message)) {
            setClosedMessage((current) => current ?? message);
          } else {
            timer = setTimeout(arm, SERVER_CHECK_RETRY_MS);
          }
        });
    };
    arm();
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [deadline, performSave]);

  // The prefilled kind goes through the same autosave as a typed answer, once.
  useEffect(() => {
    if (prefilled !== null) {
      onChange(prefilled);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  /** Whatever autosave is still owed, before the answers it is holding are
   * allowed to become the ones that get submitted. A confirmed submit that
   * skipped this could finalize the server's previous, now outdated answers
   * even though the summary screen just showed the new ones. */
  const flushPendingSave = useCallback(async (): Promise<void> => {
    if (saveTimer.current !== null) {
      clearTimeout(saveTimer.current);
      saveTimer.current = null;
      await performSave(answers);
      return;
    }

    if (pendingSave.current !== null) {
      await pendingSave.current;
    }
  }, [answers, performSave]);

  useFieldFocus(focusTarget, activeSectionKey, clearFocusTarget);

  const jumpToGap = useCallback((gap: SubmissionGap) => {
    setStage("filling");
    // An empty sectionKey is the attachments gap, not a form section: it has
    // nothing to set FormRenderer's active section to, and setting one that
    // does not exist would blank the form out entirely.
    if (gap.sectionKey !== "") {
      selectSection(gap.sectionKey);
    }
    setFocusTarget(gap.anchorId);
  }, [selectSection]);

  /** "Popraw" on the summary screen: the section only, no specific field to
   * focus, unlike a gap which always names one. */
  const goToSection = useCallback(
    (sectionKey: string) => {
      setStage("filling");
      selectSection(sectionKey);
    },
    [selectSection],
  );

  async function handleConfirmedSubmit() {
    setSubmitting(true);
    setSubmitError(null);

    try {
      await flushPendingSave();
      onSubmitted(await submitApplication(application.id));
    } catch (error) {
      const message = apiErrorMessage(error, "Nie udało się złożyć wniosku.");
      setSubmitError(message);
      if (isFinalRefusal(error, message)) {
        setClosedMessage(message);
      }
      setSubmitting(false);
    }
  }

  return (
    // Two columns from a laptop up: the form, and beside it what has to stay
    // in view while it is filled in (the deadline, the way to submit, the
    // checksum). In the DOM the side comes first, the order this screen has
    // always had, so a phone and a screen reader meet the deadline first.
    <div className="grid items-start gap-8 lg:grid-cols-[minmax(0,1fr)_20rem]">
      <aside aria-label="Stan wniosku" className="flex flex-col gap-4 lg:sticky lg:top-6 lg:col-start-2 lg:row-start-1 lg:max-h-[calc(100vh-3rem)] lg:overflow-y-auto">
      {saving ? (
        <p className="text-sm text-text-muted">Zapisywanie…</p>
      ) : sessionLost ? (
        <div role="alert" className="flex flex-col gap-2 text-sm text-brand-accent-text">
          <p>
            Nie zapisano ostatnich zmian, bo sesja się zakończyła. Odpowiedzi zostają w formularzu, nie zamykaj tej
            karty.
          </p>
          <p>
            <a
              className="underline"
              href={withReturnUrl(loginPath, `/panel/applicant/applications/${application.id}`)}
              target="_blank"
              rel="noopener"
            >
              Zaloguj się ponownie (w nowej karcie)
            </a>
            , a potem wróć tutaj i{" "}
            <button type="button" className="underline" onClick={() => void performSave(answers).catch(() => {})}>
              zapisz zmiany
            </button>
            .
          </p>
        </div>
      ) : saveError !== null ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {saveError}
        </p>
      ) : (
        <p className="text-sm text-text-muted">Zapisano o {formatTimeOnly(application.lastSavedAt)}</p>
      )}

      {correction ? (
        <section aria-labelledby="zwrot-tytul" className="flex flex-col gap-3 rounded-lg border border-status-attention-text bg-status-attention-bg p-5 text-status-attention-text">
          <h2 id="zwrot-tytul" className="text-xl">
            Wniosek zwrócony do poprawy
          </h2>
          <p className="text-sm">{correction.message}</p>
          <p className="text-sm">
            Zmienić możesz tylko sekcje:{" "}
            {form.document.sections
              .filter((section) => correction.sections.includes(section.key))
              .map((section) => section.title)
              .join(", ")}
            {correction.unlocksAttachments ? ", oraz załączniki" : ""}. Pozostałe części wniosku są zablokowane.
          </p>
          <IntakeCountdown
            closesAt={correction.deadline}
            message={`Poprawiony wniosek złóż ponownie do ${formatMoment(correction.deadline)}.`}
          />
        </section>
      ) : (
        <div className="rounded-lg border border-border bg-surface-warm p-5">
          <IntakeCountdown
            closesAt={competition.intake.acceptsApplications ? competition.intake.closesAt : null}
            message={competition.intake.message}
          />
        </div>
      )}

      {closedMessage !== null ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {closedMessage} Wersja robocza zostaje zapisana.
        </p>
      ) : null}

      <SubmitBar
        closed={closedMessage !== null}
        gaps={gaps}
        onJump={jumpToGap}
        onContinue={() => (stage === "filling" ? setStage("reviewing") : setConfirmOpen(true))}
      />

      <TechnicalBlock application={application} versionNumber={form.versionNumber} />
      </aside>

      <div className="flex min-w-0 flex-col gap-8 lg:col-start-1 lg:row-start-1">
      {stage === "filling" ? (
        <>
          <FormRenderer
            document={shownDocument}
            initialAnswers={answers}
            competitionSettings={competitionSettings}
            onChange={onChange}
            activeSectionKey={activeSectionKey}
            onActiveSectionChange={selectSection}
          />

          {correction && !correction.unlocksAttachments ? (
            <p className="text-sm">Załączniki nie są odblokowane do poprawy.</p>
          ) : (
          <AttachmentsPanel
            applicationId={application.id}
            requirements={competition.attachments}
            attachments={attachments}
            onUploaded={(attachment) => setAttachments((previous) => [...previous, attachment])}
            onReplaced={(replacedId, attachment) =>
              setAttachments((previous) =>
                previous.map((existing) => (existing.id === replacedId ? attachment : existing)),
              )
            }
            onWithdrawn={(withdrawnId) =>
              setAttachments((previous) => previous.filter((existing) => existing.id !== withdrawnId))
            }
          />
          )}
        </>
      ) : (
        <>
          <h2 className="text-3xl">Podsumowanie wniosku</h2>
          <OfferView document={shownDocument} answers={answers} onEditSection={goToSection} />
        </>
      )}

      </div>

      {confirmOpen ? (
        <ConfirmSubmitDialog
          submitting={submitting}
          error={submitError}
          final={closedMessage !== null}
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
