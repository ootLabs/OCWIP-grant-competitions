"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

import { apiErrorMessage } from "@/lib/api-client";
import { startReport } from "@/lib/reports";

import { applicantPanelRoot } from "../../navigation";

/**
 * The way into the report of a funded project (T-50a). Starting is also
 * opening: the API hands back the report already started, so the same button
 * serves the first visit and every one after it.
 */
export function ReportEntry({ applicationId }: { applicationId: string }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function open() {
    setBusy(true);
    setError(null);
    try {
      const report = await startReport(applicationId);
      router.push(`${applicantPanelRoot}/reports/${report.id}`);
    } catch (failure) {
      setError(apiErrorMessage(failure, "Nie udało się otworzyć sprawozdania."));
      setBusy(false);
    }
  }

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xl">Sprawozdanie</h2>
      <p className="text-sm">Po zakończeniu projektu złóż sprawozdanie z jego realizacji.</p>
      <div>
        <button
          type="button"
          className="rounded-sm bg-brand-accent px-4 py-2 text-sm text-bg hover:bg-brand-accent-hover disabled:opacity-40"
          disabled={busy}
          onClick={() => void open()}
        >
          Przejdź do sprawozdania
        </button>
      </div>
      {error !== null ? (
        <p role="alert" className="text-sm">
          {error}
        </p>
      ) : null}
    </section>
  );
}
