"use client";

import { type FormEvent, useEffect, useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import {
  fetchCorrections,
  openReturn,
  returnApplication,
  type ApplicationCorrections,
} from "@/lib/application-corrections";
import { formatMoment } from "@/lib/format";
import type { FormDocument } from "@/lib/forms/document-types";
import { localWarsawToUtcIso } from "@/lib/local-time";
import { applicationStatusLabels, type ApplicationStatus } from "@/lib/operator-applications";

/**
 * "Zwrot do poprawy" on the operator's view of one application (T-103): the
 * form to return a submitted application (sections, attachments, note,
 * deadline), and what came of the returns so far: open or corrected, the
 * earlier versions with their checksums, the status history.
 */
export function ReturnPanel({
  applicationId,
  status,
  document,
  onReturned,
}: {
  applicationId: string;
  status: ApplicationStatus;
  document: FormDocument;
  onReturned: () => void;
}) {
  const [corrections, setCorrections] = useState<ApplicationCorrections | null>(null);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let current = true;
    fetchCorrections(applicationId)
      .then((loaded) => current && setCorrections(loaded))
      .catch(() => current && setCorrections(null));
    return () => {
      current = false;
    };
  }, [applicationId, attempt]);

  const open = corrections ? openReturn(corrections) : null;
  const titleOf = (key: string) => document.sections.find((section) => section.key === key)?.title ?? key;

  return (
    <section aria-labelledby="zwrot-naglowek" className="flex flex-col gap-3">
      <h2 id="zwrot-naglowek" className="text-xl">
        Zwrot do poprawy
      </h2>

      {open ? (
        <p className="text-sm">
          Wniosek czeka na poprawkę do {formatMoment(open.deadline)}. Sekcje:{" "}
          {open.sections.map(titleOf).join(", ")}
          {open.unlocksAttachments ? ", załączniki" : ""}.
        </p>
      ) : null}

      {status === "Submitted" ? (
        <ReturnForm
          applicationId={applicationId}
          document={document}
          onReturned={() => {
            setAttempt((value) => value + 1);
            onReturned();
          }}
        />
      ) : null}

      {corrections && corrections.versions.length > 0 ? (
        <div className="flex flex-col gap-1 text-sm">
          <h3 className="font-semibold">Wcześniejsze wersje</h3>
          <ul className="flex flex-col gap-1">
            {corrections.versions.map((version) => (
              <li key={version.versionNumber}>
                Wersja {version.versionNumber}: złożona {formatMoment(version.submittedAt)}, suma kontrolna{" "}
                <span className="font-mono">{version.checksum}</span>, zwrócona {formatMoment(version.supersededAt)}
              </li>
            ))}
          </ul>
        </div>
      ) : null}

      {corrections && corrections.history.length > 0 ? (
        <div className="flex flex-col gap-1 text-sm">
          <h3 className="font-semibold">Historia statusów</h3>
          <ul className="flex flex-col gap-1">
            {corrections.history.map((entry) => (
              <li key={`${entry.changedAt}-${entry.toStatus}`}>
                {formatMoment(entry.changedAt)}: {applicationStatusLabels[entry.fromStatus]} na{" "}
                {applicationStatusLabels[entry.toStatus]}
              </li>
            ))}
          </ul>
        </div>
      ) : null}
    </section>
  );
}

/** Also under a negative formal card (P4-17), with the card's shortcomings as the note. */
export function ReturnForm({
  applicationId,
  document,
  onReturned,
  initialMessage = "",
}: {
  applicationId: string;
  document: FormDocument;
  onReturned: () => void;
  initialMessage?: string;
}) {
  const [sections, setSections] = useState<string[]>([]);
  const [attachments, setAttachments] = useState(false);
  const [message, setMessage] = useState(initialMessage);
  const [deadline, setDeadline] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSending(true);
    setError(null);

    try {
      await returnApplication(applicationId, {
        sections,
        unlocksAttachments: attachments,
        message,
        deadline: deadline ? localWarsawToUtcIso(deadline) : null,
      });
      onReturned();
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się zwrócić wniosku."));
    } finally {
      setSending(false);
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3 text-sm">
      <fieldset className="flex flex-col gap-1">
        <legend className="font-medium">Sekcje do poprawy</legend>
        {document.sections.map((section) => (
          <label key={section.key} className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={sections.includes(section.key)}
              onChange={(event) =>
                setSections((previous) =>
                  event.target.checked ? [...previous, section.key] : previous.filter((key) => key !== section.key),
                )
              }
            />
            {section.title}
          </label>
        ))}
        <label className="flex items-center gap-2">
          <input type="checkbox" checked={attachments} onChange={(event) => setAttachments(event.target.checked)} />
          Załączniki
        </label>
      </fieldset>

      <label className="flex flex-col gap-1">
        Co poprawić (trafi do wnioskodawcy w mailu)
        <textarea
          className="rounded-sm border border-border-control px-2 py-1"
          rows={4}
          maxLength={2000}
          value={message}
          onChange={(event) => setMessage(event.target.value)}
        />
      </label>

      <label className="flex flex-col gap-1">
        Termin poprawy
        <input
          type="datetime-local"
          step={60}
          className="rounded-sm border border-border-control px-2 py-1"
          value={deadline}
          onChange={(event) => setDeadline(event.target.value)}
        />
      </label>

      {error ? (
        <p role="alert" className="text-sm">
          {error}
        </p>
      ) : null}

      <button type="submit" disabled={sending} className="self-start rounded-sm border border-border-control px-3 py-1">
        {sending ? "Zwracanie…" : "Zwróć do poprawy"}
      </button>
    </form>
  );
}
