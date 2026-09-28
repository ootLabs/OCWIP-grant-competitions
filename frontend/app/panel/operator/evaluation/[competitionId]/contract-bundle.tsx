"use client";

import { useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { bundleContracts } from "@/lib/contracts";

/**
 * "Umowy jednym kliknięciem" (T-45b): draws up the missing contracts of every
 * funded application and downloads the complete ones as one ZIP. A contract
 * with a blank left is not in it; braki.txt inside the ZIP names it.
 */
export function ContractBundle({ competitionId }: { competitionId: string }) {
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function download() {
    setBusy(true);
    setMessage(null);
    try {
      const { blob, fileName } = await bundleContracts(competitionId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = fileName ?? "umowy.zip";
      link.click();
      // Released on the next turn: revoking right after click() can cancel
      // the download before the browser has read the blob.
      setTimeout(() => URL.revokeObjectURL(url), 0);
      setMessage("Pobrano umowy. Umowy bez kompletu pól wymienia plik braki.txt w archiwum.");
    } catch (failure) {
      setMessage(apiErrorMessage(failure, "Nie udało się przygotować umów."));
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="flex flex-col gap-2 text-sm">
      <p>Sporządza brakujące umowy wszystkich dofinansowanych wniosków i pobiera komplet jako jeden plik ZIP.</p>
      <div>
        <button
          type="button"
          className="rounded-sm border border-border-control px-4 py-2 disabled:opacity-40"
          disabled={busy}
          onClick={() => void download()}
        >
          {busy ? "Przygotowywanie umów" : "Pobierz wszystkie umowy (ZIP)"}
        </button>
      </div>
      {message !== null ? <p role="status">{message}</p> : null}
    </div>
  );
}
