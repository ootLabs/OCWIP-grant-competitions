"use client";

import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";

import { CompetitionFacts } from "@/app/competitions/competition-facts";
import { statusLabels } from "@/app/competitions/labels";
import { ApiError } from "@/lib/api-client";
import { fetchOperatorCompetition, type OperatorCompetition } from "@/lib/operator-competitions";

import { ContentSection } from "./content-section";
import { CopySection } from "./copy-section";
import { StatusActions } from "./status-actions";

type Load =
  | { readonly status: "loading" }
  | { readonly status: "missing" }
  | { readonly status: "error" }
  | { readonly status: "ready"; readonly competition: OperatorCompetition };

/**
 * Strona konkursu w panelu operatora (T-97): what the competition is, what
 * it still lacks, what can happen to it next, and the way to every screen
 * that works on it (edycja, formularz, wnioski, ocena). Publication happens
 * here, not at the end of the wizard, because it needs the form and both
 * evaluation cards (CompetitionService.PublicationGaps).
 */
export default function OperatorCompetitionPage() {
  const { id } = useParams<{ id: string }>();
  const [load, setLoad] = useState<Load>({ status: "loading" });

  const [reload, setReload] = useState(0);

  useEffect(() => {
    let current = true;

    fetchOperatorCompetition(id)
      .then((competition) => current && setLoad({ status: "ready", competition }))
      .catch((error: unknown) => {
        if (current) {
          setLoad({ status: error instanceof ApiError && error.status === 404 ? "missing" : "error" });
        }
      });

    return () => {
      current = false;
    };
  }, [id, reload]);

  if (load.status === "loading") {
    return <p className="text-sm">Wczytywanie konkursu…</p>;
  }

  if (load.status !== "ready") {
    return (
      <p className="text-sm">
        {load.status === "missing" ? "Nie ma takiego konkursu." : "Nie udało się wczytać konkursu."}{" "}
        <Link className="underline" href="/panel/operator">
          Wróć do listy konkursów
        </Link>
      </p>
    );
  }

  const { competition } = load;
  const base = `/panel/operator`;

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <header className="flex flex-col gap-1">
        <p className="text-sm">
          <Link className="underline" href={base}>
            Konkursy
          </Link>{" "}
          / Nr {competition.number}
        </p>
        <h1 className="text-2xl">{competition.title}</h1>
        <p className="text-sm">
          {statusLabels[competition.status]}
          {competition.isActive ? "" : " · dezaktywowany"} · {competition.intake.message}
        </p>
      </header>

      {competition.status === "Draft" && competition.publicationGaps.length > 0 ? (
        <section className="flex flex-col gap-2 rounded-sm border border-border-muted px-4 py-3">
          <h2 className="text-xl">Przed publikacją brakuje</h2>
          <ul className="list-disc pl-5 text-sm">
            {competition.publicationGaps.map((gap) => (
              <li key={gap}>{gap}</li>
            ))}
          </ul>
          <p className="text-sm">
            Formularz układasz w{" "}
            <Link className="text-text-link underline" href={`${base}/forms/${competition.id}`}>
              kreatorze formularza
            </Link>
            . Karty oceny skopiujesz niżej z innego konkursu albo wgra je administrator razem z treścią konkursu.
          </p>
        </section>
      ) : null}

      <StatusActions
        competition={competition}
        onChanged={(updated) => setLoad({ status: "ready", competition: updated })}
      />

      <ContentSection competition={competition} onCopied={() => setReload((value) => value + 1)} />

      <CopySection competition={competition} />

      <nav aria-label="Praca nad konkursem" className="flex flex-col gap-2">
        <h2 className="text-xl">Praca nad konkursem</h2>
        <ul className="flex flex-col gap-1 text-sm">
          <li>
            <Link className="text-text-link underline" href={`${base}/competitions/${competition.id}/edit`}>
              Edytuj ogłoszenie
            </Link>
          </li>
          <li>
            <Link className="text-text-link underline" href={`${base}/forms/${competition.id}`}>
              Formularz wniosku
            </Link>
          </li>
          <li>
            <Link className="text-text-link underline" href={`${base}/applications/${competition.id}`}>
              Wnioski
            </Link>
          </li>
          <li>
            <Link className="text-text-link underline" href={`${base}/evaluation/${competition.id}`}>
              Ocena i wyniki
            </Link>
          </li>
          {competition.status !== "Draft" ? (
            <li>
              <a className="text-text-link underline" href={`/competitions/${competition.id}`} target="_blank" rel="noopener noreferrer">
                Publiczna strona konkursu
              </a>
            </li>
          ) : null}
        </ul>
      </nav>

      <section className="flex flex-col gap-2">
        <h2 className="text-xl">Terminy i kwoty</h2>
        <CompetitionFacts competition={competition} />
      </section>
    </div>
  );
}
