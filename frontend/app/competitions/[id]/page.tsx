import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";

import {
  competitionPath,
  fetchPublicCompetition,
  fetchPublicResults,
  resultsPath,
  type PublicCompetition,
} from "@/lib/competitions";
import { formatAmount, formatMoment, timeZoneLabel } from "@/lib/format";

import { ApplyLink } from "../apply-link";
import {
  CompetitionAttachments,
  CompetitionContacts,
} from "../competition-attachments";
import { CompetitionFacts } from "../competition-facts";
import { CompetitionPermalink } from "../competition-permalink";
import { IntakeCountdown } from "../intake-countdown";
import { statusLabels, statusTones } from "../labels";
import { StatusBadge } from "@/components/ui/status-badge";
import { cardClassName, highlightCardClassName } from "@/components/ui/styles";

type PageProps = { params: Promise<{ id: string }> };

/**
 * The preview somebody sees when the link is pasted into a post.
 *
 * The card asks for the title, the amount and the deadline to be visible
 * there, which is why the description is assembled rather than taken from the
 * competition's own text: an announcement that opens with two paragraphs of
 * context would show two paragraphs of context and no money and no date.
 *
 * A draft has no public address, so it gets no metadata either and the page
 * below answers 404. Anything else would put the title of an unannounced
 * competition into a link preview.
 */
export async function generateMetadata({
  params,
}: PageProps): Promise<Metadata> {
  const competition = await fetchPublicCompetition((await params).id);

  if (competition === null) {
    return { title: "Nie znaleziono konkursu" };
  }

  const description = summary(competition);

  return {
    title: competition.title,
    description,
    openGraph: {
      type: "article",
      locale: "pl_PL",
      siteName: "Konkursy OCWIP",
      title: competition.title,
      description,
      // No og:url. Next resolves it against metadataBase, which this product
      // does not know: the public address is a deployment fact, not a build
      // one. A relative og:url is worse than none, because a crawler that
      // resolves it against its own origin publishes a link to nowhere.
    },
  };
}

export default async function CompetitionPage({ params }: PageProps) {
  const id = (await params).id;
  const [competition, results] = await Promise.all([fetchPublicCompetition(id), fetchPublicResults(id)]);

  // The backend answers 404 both for a competition that never existed and for
  // one that is still a draft, and this page keeps that indistinguishable.
  if (competition === null) {
    notFound();
  }

  const { intake } = competition;

  return (
    <article className="flex flex-col gap-8">
      <header className="flex flex-col gap-3">
        <p className="flex flex-wrap items-center gap-3 text-sm text-text-muted">
          <StatusBadge tone={statusTones[competition.status]}>{statusLabels[competition.status]}</StatusBadge>
          <span>Konkurs nr {competition.number}</span>
        </p>
        <h1 className="max-w-4xl text-4xl leading-tight sm:text-5xl">{competition.title}</h1>
      </header>

      {/* Two columns from a laptop up: the way in on the right, where the eye
          returns after every section. In the DOM it comes first, so a screen
          reader and a phone get the deadline and the button before the long
          text, the same order this page always had. */}
      <div className="grid items-start gap-10 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <aside aria-label="Nabór" className="flex flex-col gap-5 lg:sticky lg:top-6 lg:col-start-2 lg:row-start-1">
          <div className={`${highlightCardClassName} flex flex-col gap-5 p-6`}>
            {/* The state of the intake, in the rule's own words, plus a
                countdown when there is something left to count. A continuous
                intake has no closing moment and a closed one has nothing left,
                so both of them get the sentence alone. */}
            <IntakeCountdown
              closesAt={intake.acceptsApplications ? intake.closesAt : null}
              message={intake.message}
            />

            <ApplyLink competitionId={competition.id} intake={intake} />

            {results === null ? null : (
              <p>
                <Link className="font-semibold text-text-link underline" href={resultsPath(competition.id)}>
                  Wyniki konkursu
                </Link>
              </p>
            )}
          </div>

          <section className={`${cardClassName} flex flex-col gap-3 p-6`}>
            <h2 className="text-xl">Kontakt w sprawie konkursu</h2>
            <CompetitionContacts contacts={competition.contacts} />
          </section>
        </aside>

        <div className="flex min-w-0 flex-col gap-12 lg:col-start-1 lg:row-start-1">
          {competition.description === null ? null : (
            <Section title="Opis konkursu">
              <Paragraphs text={competition.description} />
            </Section>
          )}

          {competition.expectedResults === null ? null : (
            <Section title="Zakładane rezultaty">
              <Paragraphs text={competition.expectedResults} />
            </Section>
          )}

          <Section title="Terminy i kwoty">
            <CompetitionFacts competition={competition} />
          </Section>

          {competition.rulesUrl === null ? null : (
            <Section title="Regulamin">
              <p>
                <a
                  className="font-semibold text-text-link underline"
                  href={competition.rulesUrl}
                  rel="noopener noreferrer"
                  target="_blank"
                >
                  Regulamin konkursu (otwiera się w nowej karcie)
                </a>
              </p>
            </Section>
          )}

          <Section title="Wymagane załączniki">
            <CompetitionAttachments attachments={competition.attachments} />
          </Section>

          <Section title="Udostępnij konkurs">
            <CompetitionPermalink path={competitionPath(competition.id)} />
          </Section>
        </div>
      </div>
    </article>
  );
}

/** Title, amount and deadline in one sentence, for a link preview. */
function summary(competition: PublicCompetition): string {
  const deadline =
    competition.intake.closesAt === null
      ? "Nabór ciągły, bez terminu końcowego."
      : `Nabór do ${formatMoment(competition.intake.closesAt)} ${timeZoneLabel()}.`;

  return `Dotacja do ${formatAmount(competition.maxGrantAmount)}. ${deadline}`;
}

function Section({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <section className="flex flex-col gap-3">
      {/* One level below the competition title and never skipping a level:
          heading structure is how a screen reader user moves around a page
          this long. */}
      <h2 className="text-2xl">{title}</h2>
      {children}
    </section>
  );
}

/**
 * Operator prose, kept as the paragraphs it was typed as.
 *
 * Rendered as text nodes, never as markup: this text comes from a form and
 * putting it through anything that interprets HTML would make the operator
 * panel a way of writing into a public page.
 */
function Paragraphs({ text }: { text: string }) {
  const paragraphs = text
    .split(/\n{2,}/)
    .map((paragraph) => paragraph.trim())
    .filter((paragraph) => paragraph.length > 0);

  return (
    <div className="flex max-w-prose flex-col gap-3">
      {paragraphs.map((paragraph, index) => (
        <p key={index} className="whitespace-pre-line">
          {paragraph}
        </p>
      ))}
    </div>
  );
}
