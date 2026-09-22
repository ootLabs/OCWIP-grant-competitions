"use client";

import { useParams } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  competitionsWithForms,
  fetchCurrentFormDocument,
  fetchOperatorCompetitions,
  type CompetitionSummary,
} from "@/lib/forms/competition-forms";
import { cloneDocument, type FormDocument } from "@/lib/forms/document-types";
import { clearDraft, loadDraft, saveDraft } from "@/lib/forms/draft-storage";
import { Builder } from "./builder";
import { PreviewPanel } from "./preview-panel";
import { PublishPanel } from "./publish-panel";
import { SourcePicker } from "./source-picker";

type State =
  | { readonly status: "loading" }
  | { readonly status: "error" }
  | { readonly status: "needs-source"; readonly sources: CompetitionSummary[] }
  | {
      readonly status: "ready";
      readonly document: FormDocument;
      readonly savedAt: string | null;
      readonly history: readonly FormDocument[];
      /** The competition this document was copied from, for the status bar. */
      readonly copiedFrom: string | null;
    };

const MAX_HISTORY = 20;

/**
 * The creator itself (T-26): loads a draft if one exists, otherwise the
 * competition's own current form, otherwise asks which competition to copy
 * one from. Every edit autosaves to the browser (lib/forms/draft-storage.ts)
 * and pushes the previous document onto an undo stack, so "zmiana kolejności
 * jest odwracalna przed zapisem" holds for every edit, not only reordering.
 *
 * The preview and publish button (T-27) live on this same screen rather than
 * a separate route: they act on the exact document the kreator has in hand,
 * draft included, which is the whole reason a podgląd here can be trusted.
 */
export default function FormBuilderPage() {
  const { competitionId } = useParams<{ competitionId: string }>();
  const [state, setState] = useState<State>({ status: "loading" });
  const [mode, setMode] = useState<"edit" | "preview">("edit");

  // The competition this page is for is fixed for the life of the page, so
  // effects below only need to run once per competitionId, not per render.
  const idRef = useRef(competitionId);
  idRef.current = competitionId;

  useEffect(() => {
    let current = true;
    setState({ status: "loading" });

    (async () => {
      const draft = loadDraft(competitionId);
      if (draft !== null) {
        if (current) {
          setState({
            status: "ready",
            document: draft.document,
            savedAt: draft.savedAt,
            history: [],
            copiedFrom: draft.copiedFromCompetitionId,
          });
        }
        return;
      }

      const ownDocument = await fetchCurrentFormDocument(competitionId);
      if (!current) {
        return;
      }

      if (ownDocument !== null) {
        setState({
          status: "ready",
          document: ownDocument,
          savedAt: null,
          history: [],
          copiedFrom: null,
        });
        return;
      }

      const competitions = await fetchOperatorCompetitions();
      if (!current) {
        return;
      }

      const sources = competitionsWithForms(competitions).filter(
        (competition) => competition.id !== competitionId,
      );
      setState({ status: "needs-source", sources });
    })().catch(() => {
      if (current) {
        setState({ status: "error" });
      }
    });

    return () => {
      current = false;
    };
  }, [competitionId]);

  const applyChange = useCallback(
    (next: FormDocument) => {
      setState((previous) => {
        if (previous.status !== "ready") {
          return previous;
        }
        const history = [...previous.history, previous.document].slice(-MAX_HISTORY);
        saveDraft(idRef.current, next, previous.copiedFrom);
        return {
          status: "ready",
          document: next,
          savedAt: new Date().toISOString(),
          history,
          copiedFrom: previous.copiedFrom,
        };
      });
    },
    [],
  );

  const onUndo = useCallback(() => {
    setState((previous) => {
      if (previous.status !== "ready" || previous.history.length === 0) {
        return previous;
      }
      const history = [...previous.history];
      const document = history.pop()!;
      saveDraft(idRef.current, document, previous.copiedFrom);
      return {
        status: "ready",
        document,
        savedAt: new Date().toISOString(),
        history,
        copiedFrom: previous.copiedFrom,
      };
    });
  }, []);

  const onDiscard = useCallback(() => {
    clearDraft(idRef.current);
    setState({ status: "loading" });
    fetchCurrentFormDocument(idRef.current)
      .then((document) => {
        if (document !== null) {
          setState({
            status: "ready",
            document,
            savedAt: null,
            history: [],
            copiedFrom: null,
          });
        } else {
          return fetchOperatorCompetitions().then((competitions) => {
            setState({
              status: "needs-source",
              sources: competitionsWithForms(competitions).filter((c) => c.id !== idRef.current),
            });
          });
        }
      })
      .catch(() => setState({ status: "error" }));
  }, []);

  const onCopy = useCallback((sourceCompetitionId: string) => {
    setState({ status: "loading" });
    fetchCurrentFormDocument(sourceCompetitionId)
      .then((document) => {
        if (document === null) {
          setState({ status: "error" });
          return;
        }
        const copy = cloneDocument(document);
        saveDraft(idRef.current, copy, sourceCompetitionId);
        setState({
          status: "ready",
          document: copy,
          savedAt: new Date().toISOString(),
          history: [],
          copiedFrom: sourceCompetitionId,
        });
      })
      .catch(() => setState({ status: "error" }));
  }, []);

  const onPublished = useCallback(() => {
    // A published version is no longer a draft in progress: the browser
    // copy stops mattering the moment the server has its own row for it,
    // and keeping it around would only make a later "own form" reload read
    // stale content back over what was just published.
    clearDraft(idRef.current);
    setState((previous) => {
      if (previous.status !== "ready") {
        return previous;
      }
      return { status: "ready", document: previous.document, savedAt: null, history: [], copiedFrom: null };
    });
  }, []);

  if (state.status === "loading") {
    return <p className="text-sm">Wczytywanie formularza…</p>;
  }

  if (state.status === "error") {
    return <p className="text-sm">Nie udało się wczytać formularza. Odśwież stronę.</p>;
  }

  if (state.status === "needs-source") {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl">Kreator formularza</h1>
        <SourcePicker sources={state.sources} onCopy={onCopy} />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl">Kreator formularza</h1>

      <div className="flex gap-2" role="tablist">
        <button
          type="button"
          role="tab"
          aria-selected={mode === "edit"}
          className={`rounded-sm border px-3 py-1.5 text-sm ${mode === "edit" ? "border-brand-accent" : "border-border"}`}
          onClick={() => setMode("edit")}
        >
          Edycja
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === "preview"}
          className={`rounded-sm border px-3 py-1.5 text-sm ${mode === "preview" ? "border-brand-accent" : "border-border"}`}
          onClick={() => setMode("preview")}
        >
          Podgląd
        </button>
      </div>

      <PublishPanel competitionId={competitionId} document={state.document} onPublished={onPublished} />

      {mode === "edit" ? (
        <Builder
          document={state.document}
          savedAt={state.savedAt}
          copiedFrom={state.copiedFrom}
          canUndo={state.history.length > 0}
          onChange={applyChange}
          onUndo={onUndo}
          onDiscard={onDiscard}
        />
      ) : (
        <PreviewPanel competitionId={competitionId} document={state.document} />
      )}
    </div>
  );
}
