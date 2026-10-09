import type { Metadata } from "next";
import Link from "next/link";

import { CompetitionCard } from "@/app/competitions/competition-card";
import { statusLabels, statusTones } from "@/app/competitions/labels";
import { EmptyState } from "@/components/empty-state";
import { PublicFrame } from "@/components/public-frame";
import { StatusBadge } from "@/components/ui/status-badge";
import {
  cardClassName,
  eyebrowClassName,
  highlightCardClassName,
  primaryActionClassName,
  secondaryActionClassName,
} from "@/components/ui/styles";
import {
  archivePath,
  competitionPath,
  fetchPublicCompetitions,
  resultsPath,
  type PublicCompetition,
} from "@/lib/competitions";
import { formatAmount, formatMoment, timeZoneLabel } from "@/lib/format";
import { registerPath } from "@/lib/login";
import { loginPath } from "@/lib/session";

export const metadata: Metadata = {
  title: "Konkursy dotacyjne | Generator konkursów OCWIP",
  description:
    "Otwarte nabory i wyniki konkursów dotacyjnych Opolskiego Centrum Wspierania Inicjatyw Pozarządowych.",
};

// Read at request time: a nabór opens and closes to the minute (T-21).
export const dynamic = "force-dynamic";

/** The path through the product, in the order the applicant walks it. */
const steps: readonly { readonly title: string; readonly text: string }[] = [
  { title: "Wybierz konkurs", text: "Warunki, kwoty i terminy przeczytasz bez zakładania konta." },
  { title: "Wypełnij wniosek", text: "Wersja robocza zapisuje się sama, a budżet liczy się automatycznie." },
  { title: "Ocena", text: "Po ocenie zobaczysz w panelu wynik i karty oceny swojego wniosku." },
  { title: "Umowa i sprawozdanie", text: "Po przyznaniu dotacji umowę i sprawozdanie znajdziesz w tym samym miejscu." },
];

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
  const closingFirst = soonestToClose(open);

  return (
    <PublicFrame width="wide">
      <div className="flex flex-col gap-14">
        <section className={`${highlightCardClassName} grid gap-8 px-6 py-10 sm:px-10 lg:grid-cols-[3fr_2fr] lg:items-center`}>
          <div className="flex flex-col gap-5">
            <p className={eyebrowClassName}>Opolskie Centrum Wspierania Inicjatyw Pozarządowych</p>
            <h1 className="text-4xl leading-tight sm:text-5xl">Konkursy dotacyjne OCWIP</h1>
            <p className="max-w-prose text-lg text-text-muted">
              Tu składasz wniosek w konkursach Opolskiego Centrum Wspierania Inicjatyw Pozarządowych,
              śledzisz jego ocenę i rozliczasz dotację. Warunki konkursów przeczytasz bez konta.
            </p>
            <p className="flex flex-wrap gap-3">
              <Link className={primaryActionClassName} href={registerPath}>
                Załóż konto
              </Link>
              <Link className={secondaryActionClassName} href={loginPath}>
                Zaloguj się
              </Link>
            </p>
          </div>

          {/* The call that closes first, the one a visitor is most likely to
              have come for. Its title is not a heading or a link here: the
              same call is listed below under its own heading, and two links
              with one name are two links a screen reader cannot tell apart. */}
          {closingFirst ? (
            <div className={`${cardClassName} flex flex-col gap-4 p-6 shadow-lg shadow-text/10`}>
              <div className="flex flex-wrap items-center justify-between gap-2">
                <StatusBadge tone={statusTones[closingFirst.status]}>{statusLabels[closingFirst.status]}</StatusBadge>
                <span className="text-sm text-text-muted">Nr {closingFirst.number}</span>
              </div>
              <p className="font-heading text-2xl font-extrabold leading-tight lining-nums">{closingFirst.title}</p>
              <dl className="grid grid-cols-2 gap-4">
                <div>
                  <dt className="text-xs text-text-muted">Dotacja do</dt>
                  <dd className="text-xl font-semibold">{formatAmount(closingFirst.maxGrantAmount)}</dd>
                </div>
                {closingFirst.totalPoolAmount == null ? null : (
                  <div>
                    <dt className="text-xs text-text-muted">Pula konkursu</dt>
                    <dd className="text-xl font-semibold">{formatAmount(closingFirst.totalPoolAmount)}</dd>
                  </div>
                )}
              </dl>
              <p className="text-sm">
                {closingFirst.intake.closesAt === null
                  ? "Nabór ciągły, bez terminu końcowego."
                  : `Nabór do ${formatMoment(closingFirst.intake.closesAt)} ${timeZoneLabel()}.`}
              </p>
              <Link className={primaryActionClassName} href={competitionPath(closingFirst.id)}>
                Zobacz szczegóły i złóż wniosek
              </Link>
            </div>
          ) : null}
        </section>

        <section className="flex flex-col gap-6">
          <h2 className="text-3xl">Jak to działa</h2>
          <ol className="grid list-none gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {steps.map((step, index) => (
              <li className={`${cardClassName} flex flex-col gap-2 p-5`} key={step.title}>
                <span aria-hidden="true" className="font-heading text-3xl font-extrabold text-brand-accent-text lining-nums">
                  {index + 1}
                </span>
                <strong className="font-semibold">{step.title}</strong>
                <span className="text-sm text-text-muted">{step.text}</span>
              </li>
            ))}
          </ol>
        </section>

        <section className="flex flex-col gap-6">
          <div className="flex flex-wrap items-end justify-between gap-3">
            <h2 className="text-3xl">Otwarte nabory</h2>
            <Link className="text-sm font-semibold text-brand-accent-text underline" href="/competitions">
              Wszystkie konkursy, także zakończone
            </Link>
          </div>
          {failed ? (
            <p className="text-sm">Nie udało się teraz pobrać listy konkursów. Odśwież stronę za chwilę.</p>
          ) : open.length === 0 ? (
            <EmptyState title="Nie ma teraz otwartego naboru" action={{ href: "/competitions", label: "Zobacz wszystkie konkursy" }}>
              Nowe konkursy pojawią się tutaj, gdy tylko ruszy nabór.
            </EmptyState>
          ) : (
            <ul className="grid list-none gap-5 md:grid-cols-2 lg:grid-cols-3">
              {open.map((competition) => (
                <CompetitionCard competition={competition} headingLevel={3} key={competition.id} />
              ))}
            </ul>
          )}
        </section>

        {resolved.length > 0 ? (
          <section className="flex flex-col gap-6">
            <h2 className="text-3xl">Wyniki</h2>
            <ul className={`${cardClassName} divide-y divide-border-muted`}>
              {resolved.map((competition) => (
                <li className="flex flex-wrap items-center gap-x-6 gap-y-2 px-5 py-4" key={competition.id}>
                  <StatusBadge tone={statusTones[competition.status]}>{statusLabels[competition.status]}</StatusBadge>
                  <Link className="flex-1 font-semibold text-text-link underline" href={resultsPath(competition.id)}>
                    Wyniki konkursu {competition.number}: {competition.title}
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ) : null}

        <p>
          <Link className="font-semibold text-text-link underline" href={archivePath}>
            Archiwum wyników
          </Link>
        </p>
      </div>
    </PublicFrame>
  );
}

/** The open call with the nearest closing time; a continuous one only when nothing else is open. */
function soonestToClose(open: readonly PublicCompetition[]): PublicCompetition | undefined {
  return [...open].sort((left, right) => {
    if (left.intake.closesAt === right.intake.closesAt) return 0;
    if (left.intake.closesAt === null) return 1;
    if (right.intake.closesAt === null) return -1;
    return left.intake.closesAt.localeCompare(right.intake.closesAt);
  })[0];
}
