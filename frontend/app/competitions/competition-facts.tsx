import type { PublicCompetition } from "@/lib/competitions";
import {
  formatAmount,
  formatDateOnly,
  formatFileSize,
  formatMoment,
  formatPercent,
  timeZoneLabel,
} from "@/lib/format";

import { costCategoryLabels, percentageBasisLabels } from "./labels";

/**
 * The numbers and dates of a competition, as a definition list (T-23).
 *
 * A definition list and not a table: these are pairs, not rows, and a table of
 * two columns collapses on a phone into something a screen reader reads as a
 * grid. Every entry is dropped when the competition has nothing in it, because
 * a competition is written over several sittings (the wizard does not block
 * moving between steps) and an empty row saying "Pula konkursu: -" reads as a
 * missing amount rather than as an amount nobody set.
 */
export function CompetitionFacts({
  competition,
}: {
  competition: PublicCompetition;
}) {
  const { intake } = competition;
  const basis = percentageBasisLabels[competition.percentageBasis];

  return (
    <dl className="flex flex-col gap-3">
      <Fact term="Nabór trwa od">
        <time dateTime={intake.opensAt}>{formatMoment(intake.opensAt)}</time>{" "}
        {timeZoneLabel()}
      </Fact>

      <Fact term="Nabór trwa do">
        {intake.closesAt === null ? (
          "nabór ciągły, bez terminu końcowego"
        ) : (
          <>
            <time dateTime={intake.closesAt}>
              {formatMoment(intake.closesAt)}
            </time>{" "}
            {timeZoneLabel()}
          </>
        )}
      </Fact>

      <Fact term="Maksymalna kwota dotacji">
        {formatAmount(competition.maxGrantAmount)}
      </Fact>

      {competition.minGrantAmount === null ? null : (
        <Fact term="Minimalna kwota dotacji">
          {formatAmount(competition.minGrantAmount)}
        </Fact>
      )}

      {competition.totalPoolAmount === null ? null : (
        <Fact term="Pula konkursu">
          {formatAmount(competition.totalPoolAmount)}
        </Fact>
      )}

      {competition.maxIndirectCostPercent === null ? null : (
        <Fact term="Maksymalny udział kosztów pośrednich">
          {formatPercent(competition.maxIndirectCostPercent)} {basis}
        </Fact>
      )}

      {competition.maxInstitutionalDevelopmentPercent === null ? null : (
        <Fact term="Maksymalny udział rozwoju instytucjonalnego">
          {formatPercent(competition.maxInstitutionalDevelopmentPercent)} {basis}
        </Fact>
      )}

      {competition.maxAverageAnnualRevenue === null ? null : (
        <Fact term="Próg średniego rocznego przychodu">
          {formatAmount(competition.maxAverageAnnualRevenue)}
        </Fact>
      )}

      {competition.projectStartDate === null &&
      competition.projectEndDate === null ? null : (
        <Fact term="Projekt realizowany w okresie">
          {competition.projectStartDate === null
            ? "od dnia podpisania umowy"
            : `od ${formatDateOnly(competition.projectStartDate)}`}{" "}
          {competition.projectEndDate === null
            ? ""
            : `do ${formatDateOnly(competition.projectEndDate)}`}
        </Fact>
      )}

      {competition.costCategories.length === 0 ? null : (
        <Fact term="Kategorie kosztów w budżecie">
          {competition.costCategories
            .map((category) => costCategoryLabels[category])
            .join(", ")}
        </Fact>
      )}

      <Fact term="Forma dostarczenia">
        {competition.requiresPaperSubmission
          ? "wersja elektroniczna oraz papierowa"
          : "wyłącznie wersja elektroniczna"}
      </Fact>

      {competition.requiresPaperSubmission &&
      competition.paperSubmissionDeadline !== null ? (
        <Fact term="Wersję papierową dostarcz do">
          <time dateTime={competition.paperSubmissionDeadline}>
            {formatMoment(competition.paperSubmissionDeadline)}
          </time>{" "}
          {timeZoneLabel()}
        </Fact>
      ) : null}

      {competition.requiresPaperSubmission &&
      competition.paperSubmissionAddress !== null ? (
        <Fact term="Adres dla wersji papierowej">
          {competition.paperSubmissionAddress}
        </Fact>
      ) : null}

      <Fact term="Limit rozmiaru pojedynczego załącznika">
        {formatFileSize(competition.maxAttachmentSizeInBytes)}
      </Fact>

      <Fact term="Limit rozmiaru całego wniosku">
        {formatFileSize(competition.maxApplicationSizeInBytes)}
      </Fact>

      {competition.personalDataProcessedUntil === null ? null : (
        <Fact term="Dane osobowe przetwarzane do">
          {formatDateOnly(competition.personalDataProcessedUntil)}
        </Fact>
      )}
    </dl>
  );
}

function Fact({
  term,
  children,
}: {
  term: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex flex-col gap-0.5 sm:flex-row sm:gap-2">
      <dt className="font-semibold sm:w-72 sm:shrink-0">{term}</dt>
      <dd>{children}</dd>
    </div>
  );
}
