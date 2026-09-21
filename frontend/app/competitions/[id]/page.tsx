import type { Metadata } from "next";
import { notFound } from "next/navigation";

import {
  competitionPath,
  fetchPublicCompetition,
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
import { statusLabels } from "../labels";

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
  const competition = await fetchPublicCompetition((await params).id);

  // The backend answers 404 both for a competition that never existed and for
  // one that is still a draft, and this page keeps that indistinguishable.
  if (competition === null) {
    notFound();
  }

  const { intake } = competition;

  return (
    <article className="flex flex-col gap-8">
      <header className="flex flex-col gap-3">
        <p className="text-sm">
          Nr {competition.number} · {statusLabels[competition.status]}
        </p>
        <h1 className="text-3xl">{competition.title}</h1>
      </header>

      {/* The state of the intake, in the rule's own words, plus a countdown
          when there is something left to count. A continuous intake has no
          closing moment and a closed one has nothing left, so both of them
          get the sentence alone. */}
      <IntakeCountdown
        closesAt={intake.acceptsApplications ? intake.closesAt : null}
        message={intake.message}
      />

      <ApplyLink competitionId={competition.id} intake={intake} />

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

      {competition.rulesUrl === null ? null : (
        <Section title="Regulamin">
          <p>
            <a
              className="text-text-link underline"
              href={competition.rulesUrl}
              rel="noopener noreferrer"
              target="_blank"
            >
              Regulamin konkursu (otwiera się w nowej karcie)
            </a>
          </p>
        </Section>
      )}

      <Section title="Terminy i kwoty">
        <CompetitionFacts competition={competition} />
      </Section>

      <Section title="Wymagane załączniki">
        <CompetitionAttachments attachments={competition.attachments} />
      </Section>

      <Section title="Kontakt w sprawie konkursu">
        <CompetitionContacts contacts={competition.contacts} />
      </Section>

      <Section title="Udostępnij konkurs">
        <CompetitionPermalink path={competitionPath(competition.id)} />
      </Section>
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
      <h2 className="text-xl">{title}</h2>
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
