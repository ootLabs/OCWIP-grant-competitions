"use client";

import { useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { templateDownloadUrl, uploadTemplate, withdrawTemplate } from "@/lib/attachment-templates";
import type { OperatorCompetition } from "@/lib/operator-competitions";

/**
 * Wzory załączników (T-102): for each requirement of the competition, the
 * file the applicant downloads, fills in and uploads back. Upload replaces
 * the one in force; withdrawal stops offering it. The page reloads the
 * competition afterwards, so the link shown is the server's.
 */
export function TemplatesSection({ competition, onChanged }: { competition: OperatorCompetition; onChanged: () => void }) {
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  async function act(requirementId: string, action: () => Promise<unknown>, failure: string) {
    setBusy(requirementId);
    setError(null);
    try {
      await action();
      onChanged();
    } catch (problem) {
      setError(apiErrorMessage(problem, failure));
    } finally {
      setBusy(null);
    }
  }

  if (competition.attachments.length === 0) {
    return null;
  }

  return (
    <section aria-labelledby="wzory" className="flex flex-col gap-2">
      <h2 id="wzory" className="text-xl">
        Wzory załączników
      </h2>
      <p className="text-sm">
        Plik do pobrania przy wymogu, na przykład wzór oświadczenia. Wnioskodawca pobiera go ze strony konkursu bez
        logowania. Do 10 MB, format sprawdzany po zawartości pliku.
      </p>
      {error ? <p role="alert" className="text-sm">{error}</p> : null}
      <ul className="flex flex-col gap-3 text-sm">
        {competition.attachments.map((attachment) => (
          <li key={attachment.id} className="flex flex-col gap-1 rounded-sm border border-border-muted px-3 py-2">
            <p className="font-medium">{attachment.title}</p>
            {attachment.template ? (
              <p>
                Wzór:{" "}
                <a className="underline" href={templateDownloadUrl(attachment.id)}>
                  {attachment.template.fileName}
                </a>{" "}
                <button
                  type="button"
                  disabled={busy === attachment.id}
                  className="underline"
                  onClick={() => act(attachment.id, () => withdrawTemplate(attachment.id), "Nie udało się wycofać wzoru.")}
                >
                  Wycofaj wzór {attachment.title}
                </button>
              </p>
            ) : (
              <p>Bez wzoru.</p>
            )}
            <label className="flex flex-col gap-1">
              {attachment.template ? "Podmień wzór" : "Wgraj wzór"} {attachment.title}
              <input
                type="file"
                disabled={busy === attachment.id}
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  if (file) {
                    void act(attachment.id, () => uploadTemplate(attachment.id, file), "Nie udało się wgrać wzoru.");
                  }
                }}
              />
            </label>
          </li>
        ))}
      </ul>
    </section>
  );
}
