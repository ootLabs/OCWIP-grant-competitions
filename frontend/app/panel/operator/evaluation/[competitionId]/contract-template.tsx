"use client";

import { useEffect, useId, useState } from "react";

import { ApiError, apiErrorMessage } from "@/lib/api-client";
import { fetchContractTemplate, publishContractTemplate, systemPlaceholders, type ContractTemplate } from "@/lib/contracts";

/**
 * The contract template of the competition (T-45): fixed text with
 * {{placeholders}}. The system fills the names listed beside it; any other
 * name becomes a blank the operator types in for each contract. Publishing
 * adds a version; contracts already drawn up keep theirs.
 *
 * A part of the text may belong to some kinds of applicant only, which is
 * how the register clause stays out of an informal group's contract. The
 * syntax is explained on the screen, because an operator writing the text is
 * the only person who can use it.
 */
export function ContractTemplateEditor({ competitionId }: { competitionId: string }) {
  const [current, setCurrent] = useState<ContractTemplate | null>(null);
  const [body, setBody] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const id = useId();

  useEffect(() => {
    let active = true;
    fetchContractTemplate(competitionId)
      .then((template) => {
        if (!active) return;
        setCurrent(template);
        setBody(template.body);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [competitionId]);

  async function publish() {
    setBusy(true);
    setMessage(null);
    try {
      const published = await publishContractTemplate(competitionId, body);
      setCurrent(published);
      setMessage(`Opublikowano wersję ${published.versionNumber}.`);
    } catch (failure) {
      setMessage(
        failure instanceof ApiError && failure.fieldErrors.body
          ? failure.fieldErrors.body.join(" ")
          : apiErrorMessage(failure, "Nie udało się opublikować wzoru."),
      );
    } finally {
      setBusy(false);
    }
  }

  const manual = current?.placeholders.filter((placeholder) => !placeholder.system) ?? [];

  return (
    <div className="flex flex-col gap-3 text-sm">
      <p>
        {current ? `Obowiązuje wersja ${current.versionNumber}.` : "Konkurs nie ma jeszcze wzoru umowy."} Znaczniki piszesz w
        podwójnych nawiasach, na przykład {"{{numer_rachunku}}"}.
      </p>
      <p>
        Fragment tylko dla części wnioskodawców zamykasz w {"{{#Organisation,PatronInformalGroup}}"} i {"{{/}}"}:
        w umowie grupy nieformalnej taki fragment się nie drukuje, a jego pola nie są wymagane. Rodzaje to{" "}
        <code>Organisation</code>, <code>PatronInformalGroup</code> i <code>InformalGroup</code>.
      </p>
      <label htmlFor={id} className="flex flex-col gap-1">
        Treść wzoru umowy
        <textarea
          id={id}
          rows={14}
          className="rounded-sm border border-border-control px-2 py-1 font-mono"
          value={body}
          onChange={(event) => setBody(event.target.value)}
        />
      </label>
      <details>
        <summary className="cursor-pointer underline">Znaczniki, które system wypełnia sam</summary>
        <ul className="mt-1 flex flex-col gap-0.5">
          {systemPlaceholders.map((placeholder) => (
            <li key={placeholder.name}>
              <code>{`{{${placeholder.name}}}`}</code>: {placeholder.label}
            </li>
          ))}
        </ul>
      </details>
      {manual.length > 0 ? (
        <p>Do wpisania przy każdej umowie: {manual.map((placeholder) => placeholder.label).join(", ")}.</p>
      ) : null}
      <div>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          disabled={busy || body.trim() === "" || body === current?.body}
          onClick={() => void publish()}
        >
          Opublikuj nową wersję wzoru
        </button>
      </div>
      {message !== null ? <p role="status">{message}</p> : null}
    </div>
  );
}
