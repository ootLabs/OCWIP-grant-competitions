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
    };

const MAX_HISTORY = 20;

/**
 * The creator itself (T-26): loads a draft if one exists, otherwise the
 * competition's own current form, otherwise asks which competition to copy
 * one from. Every edit autosaves to the browser (lib/forms/draft-storage.ts)
 * and pushes the previous document onto an undo stack, so "zmiana kolejności
 * jest odwracalna przed zapisem" holds for every edit, not only reordering.
 *
 * Nothing here calls the publish endpoint. That is T-27, reading whatever
 * this screen leaves in the draft.
 */
export default function FormBuilderPage() {
  const { competitionId } = useParams<{ competitionId: string }>();
  const [state, setState] = useState<State>({ status: "loading" });

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
          setState({ status: "ready", document: draft.document, savedAt: draft.savedAt, history: [] });
        }
        return;
      }

      const ownDocument = await fetchCurrentFormDocument(competitionId);
      if (!current) {
        return;
      }

      if (ownDocument !== null) {
        setState({ status: "ready", document: ownDocument, savedAt: null, history: [] });
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
        saveDraft(idRef.current, next, null);
        return { status: "ready", document: next, savedAt: new Date().toISOString(), history };
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
      saveDraft(idRef.current, document, null);
      return { status: "ready", document, savedAt: new Date().toISOString(), history };
    });
  }, []);

  const onDiscard = useCallback(() => {
    clearDraft(idRef.current);
    setState({ status: "loading" });
    fetchCurrentFormDocument(idRef.current)
      .then((document) => {
        if (document !== null) {
          setState({ status: "ready", document, savedAt: null, history: [] });
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
        setState({ status: "ready", document: copy, savedAt: new Date().toISOString(), history: [] });
      })
      .catch(() => setState({ status: "error" }));
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
      <Builder
        document={state.document}
        savedAt={state.savedAt}
        canUndo={state.history.length > 0}
        onChange={applyChange}
        onUndo={onUndo}
        onDiscard={onDiscard}
      />
    </div>
  );
}
