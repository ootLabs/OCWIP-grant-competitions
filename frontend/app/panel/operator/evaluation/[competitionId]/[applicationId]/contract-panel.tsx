"use client";

import { useEffect, useId, useState } from "react";

import { ConfirmDialog } from "@/components/confirm-dialog";
import { ApiError, apiErrorMessage } from "@/lib/api-client";
import {
  contractPdfUrl,
  drawUpContract,
  fetchApplicationContract,
  saveContractValues,
  signContract,
  type Contract,
} from "@/lib/contracts";
import { formatDateOnly } from "@/lib/format";

/**
 * The contract of a funded application for the operator (T-45): draw it up
 * on the template in force, type in the blanks the template leaves, print,
 * and record the day it was signed, which is allowed only when nothing is
 * blank and freezes the values.
 */
export function ContractPanel({ applicationId }: { applicationId: string }) {
  const [contract, setContract] = useState<Contract | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [signedOn, setSignedOn] = useState("");
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const dateId = useId();

  function show(loaded: Contract) {
    setContract(loaded);
    setValues(
      Object.fromEntries(loaded.fields.filter((field) => !field.system).map((field) => [field.name, field.value ?? ""])),
    );
  }

  useEffect(() => {
    let active = true;
    fetchApplicationContract(applicationId)
      .then((loaded) => {
        if (active) show(loaded);
      })
      .catch(() => undefined);
    return () => {
      active = false;
    };
  }, [applicationId]);

  async function run(action: () => Promise<Contract>, done: string) {
    setBusy(true);
    setMessage(null);
    try {
      show(await action());
      setMessage(done);
      setConfirming(false);
    } catch (failure) {
      setMessage(
        failure instanceof ApiError && Object.keys(failure.fieldErrors).length > 0
          ? Object.values(failure.fieldErrors).flat().join(" ")
          : apiErrorMessage(failure, "Nie udało się zapisać umowy."),
      );
      setConfirming(false);
    } finally {
      setBusy(false);
    }
  }

  if (contract === null) {
    return (
      <section aria-labelledby="umowa" className="flex flex-col gap-2 text-sm">
        <h2 id="umowa" className="text-xl">
          Umowa
        </h2>
        <div>
          <button
            type="button"
            className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover disabled:opacity-40"
            disabled={busy}
            onClick={() => void run(() => drawUpContract(applicationId), "Umowa przygotowana.")}
          >
            Przygotuj umowę
          </button>
        </div>
        {message !== null ? <p role="status">{message}</p> : null}
      </section>
    );
  }

  const signed = contract.status === "Signed";

  return (
    <section aria-labelledby="umowa" className="flex flex-col gap-3 text-sm">
      <h2 id="umowa" className="text-xl">
        Umowa
      </h2>
      <p>
        Wzór w wersji {contract.templateVersion}.{" "}
        {signed && contract.signedOn ? `Podpisana ${formatDateOnly(contract.signedOn)}.` : "Jeszcze niepodpisana."}{" "}
        <a className="underline" href={contractPdfUrl(contract.id)}>
          Pobierz umowę (PDF)
        </a>
      </p>

      <dl className="grid grid-cols-1 gap-x-4 gap-y-2 sm:grid-cols-[max-content_1fr]">
        {contract.fields.map((field) =>
          field.system || signed ? (
            <div key={field.name} className="contents">
              <dt>{field.label}</dt>
              <dd>{(field.system ? field.value : values[field.name]) || "(puste, drukuje się jako kropki)"}</dd>
            </div>
          ) : (
            <div key={field.name} className="contents">
              <dt>
                <label htmlFor={`contract-${field.name}`}>{field.label}</label>
              </dt>
              <dd>
                <input
                  id={`contract-${field.name}`}
                  className="w-full rounded-sm border border-border-control px-2 py-1"
                  maxLength={2000}
                  value={values[field.name] ?? ""}
                  onChange={(event) => setValues({ ...values, [field.name]: event.target.value })}
                />
              </dd>
            </div>
          ),
        )}
      </dl>

      {signed ? null : (
        <div className="flex flex-wrap items-end gap-4">
          <button
            type="button"
            className="underline disabled:opacity-40"
            disabled={busy}
            onClick={() => void run(() => saveContractValues(contract.id, values), "Zapisano wartości umowy.")}
          >
            Zapisz wartości
          </button>
          <label htmlFor={dateId} className="flex flex-col gap-1">
            Data podpisania
            <input
              id={dateId}
              type="date"
              className="rounded-sm border border-border-control px-2 py-1"
              value={signedOn}
              onChange={(event) => setSignedOn(event.target.value)}
            />
          </label>
          <button
            type="button"
            className="rounded-sm bg-brand-accent px-4 py-2 text-bg hover:bg-brand-accent-hover disabled:opacity-40"
            disabled={busy || signedOn === ""}
            onClick={() => setConfirming(true)}
          >
            Zapisz podpisanie umowy
          </button>
        </div>
      )}

      {message !== null ? <p role="status">{message}</p> : null}

      {confirming ? (
        <ConfirmDialog
          title="Zapisać podpisanie umowy? Wniosek przejdzie w stan „umowa podpisana”, a wartości umowy nie będzie można już zmienić."
          confirmLabel="Zapisz podpisanie"
          busyLabel="Zapisywanie…"
          busy={busy}
          error={null}
          onCancel={() => setConfirming(false)}
          onConfirm={() =>
            void run(async () => {
              await saveContractValues(contract.id, values);
              return signContract(contract.id, signedOn);
            }, "Zapisano podpisanie umowy.")
          }
        />
      ) : null}
    </section>
  );
}
