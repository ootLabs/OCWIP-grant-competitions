import type { Metadata } from "next";
import Link from "next/link";

import { CompetitionCard } from "@/app/competitions/competition-card";
import { EmptyState } from "@/components/empty-state";
import { PublicFrame } from "@/components/public-frame";
import { statusActionClassName } from "@/components/status-page";
import { fetchPublicCompetitions, resultsPath, type PublicCompetition } from "@/lib/competitions";
import { registerPath } from "@/lib/login";
import { loginPath } from "@/lib/session";

export const metadata: Metadata = {
  title: "Konkursy dotacyjne OCWIP",
  description:
    "Otwarte nabory i wyniki konkursów dotacyjnych Opolskiego Centrum Wspierania Inicjatyw Pozarządowych.",
};

// Read at request time: a nabór opens and closes to the minute (T-21).
export const dynamic = "force-dynamic";

/**
 * The home page (T-99): what is open now, what has been decided, and the
 * way in. Rendered on the server from the same public list as /competitions
 * (D6), so it works without JavaScript and in a link preview.
 *
 * "Wyniki" are the resolved competitions: since T-97 a competition is
 * resolved exactly when its results are approved, and approved results are
 * what the public results page shows.
 */
export default async function HomePage() {
  let competitions: PublicCompetition[] = [];
  let failed = false;
  try {
    competitions = await fetchPublicCompetitions();
  } catch {
    failed = true;
  }

  const open = competitions.filter((competition) => competition.intake.acceptsApplications);
  const resolved = competitions.filter((competition) => competition.status === "Resolved");

  return (
    <PublicFrame>
      <div className="flex flex-col gap-8">
        <section className="flex flex-col gap-3">
          <h1 className="text-3xl">Konkursy dotacyjne OCWIP</h1>
          <p className="max-w-prose">
            Tu składasz wniosek w konkursach Opolskiego Centrum Wspierania Inicjatyw Pozarządowych,
            śledzisz jego ocenę i rozliczasz dotację. Warunki konkursów przeczytasz bez konta.
          </p>
          <p className="flex flex-wrap gap-3">
            <Link className={statusActionClassName} href={registerPath}>
              Załóż konto
            </Link>
            <Link className={statusActionClassName} href={loginPath}>
              Zaloguj się
            </Link>
          </p>
        </section>

        <section className="flex flex-col gap-4">
          <h2 className="text-2xl">Otwarte nabory</h2>
          {failed ? (
            <p className="text-sm">Nie udało się teraz pobrać listy konkursów. Odśwież stronę za chwilę.</p>
          ) : open.length === 0 ? (
            <EmptyState title="Nie ma teraz otwartego naboru" action={{ href: "/competitions", label: "Zobacz wszystkie konkursy" }}>
              Nowe konkursy pojawią się tutaj, gdy tylko ruszy nabór.
            </EmptyState>
          ) : (
            <ul className="flex list-none flex-col gap-4">
              {open.map((competition) => (
                <CompetitionCard competition={competition} headingLevel={3} key={competition.id} />
              ))}
            </ul>
          )}
        </section>

        {resolved.length > 0 ? (
          <section className="flex flex-col gap-2">
            <h2 className="text-2xl">Wyniki</h2>
            <ul className="flex flex-col gap-1">
              {resolved.map((competition) => (
                <li key={competition.id}>
                  <Link className="text-text-link underline" href={resultsPath(competition.id)}>
                    Wyniki konkursu {competition.number}: {competition.title}
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <p>
          <Link className="text-text-link underline" href="/competitions">
            Wszystkie konkursy, także zakończone
          </Link>
        </p>
      </div>
    </PublicFrame>
  );
}
