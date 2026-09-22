"use client";

import { useEffect, useState } from "react";
import { FormRenderer } from "@/components/form-renderer/form-renderer";
import { fetchCompetitionLimitSettings } from "@/lib/forms/competition-forms";
import type { CompetitionLimitSettings } from "@/lib/forms/limits";
import type { FormDocument } from "@/lib/forms/document-types";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly settings: CompetitionLimitSettings };

/**
 * "Dokładnie w takiej formie, w jakiej zobaczy go wnioskodawca" (T-27):
 * the same `FormRenderer` T-28 built, on the draft document sitting in the
 * kreator, never on a copy of it. Nothing here is saved anywhere; `resetKey`
 * remounts the renderer with empty answers, which is the whole mechanism
 * behind "da się przejść do końca bez zapisywania czegokolwiek jako wniosek"
 * (there is nowhere to save to, and no onChange is even wired up).
 */
export function PreviewPanel({
  competitionId,
  document,
}: {
  competitionId: string;
  document: FormDocument;
}) {
  const [load, setLoad] = useState<Load>({ status: "loading" });
  const [resetKey, setResetKey] = useState(0);

  useEffect(() => {
    let current = true;
    setLoad({ status: "loading" });

    fetchCompetitionLimitSettings(competitionId)
      .then((settings) => {
        if (current) {
          setLoad({ status: "ready", settings });
        }
      })
      .catch(() => {
        if (current) {
          setLoad({ status: "error" });
        }
      });

    return () => {
      current = false;
    };
  }, [competitionId]);

  if (load.status === "loading") {
    return <p className="text-sm">Wczytywanie podglądu…</p>;
  }

  if (load.status === "error") {
    return <p className="text-sm">Nie udało się wczytać ustawień konkursu dla podglądu.</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between gap-2 rounded-sm border border-border-muted bg-surface-muted px-3 py-2 text-sm">
        <p>To jest podgląd. Nic tu wpisane nie zostaje zapisane jako wniosek.</p>
        <button
          type="button"
          className="underline"
          onClick={() => setResetKey((value) => value + 1)}
        >
          Zacznij podgląd od nowa
        </button>
      </div>

      <FormRenderer key={resetKey} document={document} competitionSettings={load.settings} />
    </div>
  );
}
