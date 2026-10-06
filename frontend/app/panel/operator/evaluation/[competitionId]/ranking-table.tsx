"use client";

import Link from "next/link";
import { useState } from "react";

import { StatusBadge } from "@/components/ui/status-badge";
import { tableHeadCellClassName } from "@/components/ui/styles";
import { formatAmount } from "@/lib/format";
import { applicationStatusLabels, applicationStatusTones } from "@/lib/operator-applications";
import {
  formalLabels,
  formalTones,
  type CompetitionAssignment,
  type RankingRow,
  type ReviewerSummary,
} from "@/lib/operator-evaluation";

import { operatorPanelRoot } from "../../navigation";
import { AssignedExperts } from "./assigned-experts";
import { DecisionCells } from "./decision-cells";
import { GroupAssign } from "./group-assign";

const number = (value: number | string | null | undefined): number | null =>
  value === null || value === undefined ? null : Number(value);

const points = (value: number | string | null | undefined) => {
  const parsed = number(value);
  return parsed === null ? "" : String(parsed);
};

/**
 * The ranking list (T-39) with the evaluation progress next to every row and
 * the experts assigned to it (T-41). A place is shown only for an application
 * that qualifies; the rest follow with what they still wait for. The
 * awarded amount and the note (T-42) are edited in the row, next to the
 * requested and the recommended amount, until the results are approved.
 */
export function RankingTable({
  competitionId,
  rows,
  reviewers,
  assignments,
  onAssign,
  onUnassign,
  locked,
  onDecide,
}: {
  competitionId: string;
  rows: readonly RankingRow[];
  reviewers: readonly ReviewerSummary[];
  assignments: readonly CompetitionAssignment[];
  onAssign: (
    applicationIds: readonly string[],
    reviewerId: string,
  ) => Promise<boolean>;
  onUnassign: (applicationId: string, reviewerId: string) => Promise<unknown>;
  locked: boolean;
  onDecide: (applicationId: string, awardedGrant: number | null, note: string | null) => Promise<unknown>;
}) {
  const [selected, setSelected] = useState<ReadonlySet<string>>(new Set());
  const toggle = (applicationId: string) =>
    setSelected((current) => {
      const next = new Set(current);
      if (!next.delete(applicationId)) next.add(applicationId);
      return next;
    });
  const allSelected =
    rows.length > 0 && rows.every((row) => selected.has(row.applicationId));
  const isAssigned = (applicationId: string, reviewerId: string) =>
    assignments.some(
      (a) => a.applicationId === applicationId && a.reviewerId === reviewerId,
    );

  async function assignSelected(reviewerId: string) {
    // An application the expert already has is skipped rather than refused.
    // The selection stays after a refusal, so the rest can be sent again.
    const done = await onAssign(
      rows
        .map((row) => row.applicationId)
        .filter((id) => selected.has(id) && !isAssigned(id, reviewerId)),
      reviewerId,
    );
    if (done) setSelected(new Set());
  }

  const names = new Map(
    reviewers.map((reviewer) => [reviewer.id, reviewer.name || reviewer.email]),
  );

  return (
    <div className="flex flex-col gap-3">
      <GroupAssign
        selectedCount={selected.size}
        reviewers={reviewers}
        onAssign={assignSelected}
      />
      {/* relative: the cells carry sr-only labels, which are absolutely
          positioned. Without a positioned box here they are placed against
          the page, past the right edge of the scrolled table, and widen the
          whole document by the width of the table. */}
      <div className="relative overflow-x-auto rounded-lg border border-border">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">
            Lista rankingowa z postępem oceny i przypisanymi ekspertami
          </caption>
          <thead>
            <tr>
              <th scope="col" className={`${tableHeadCellClassName} text-left`}>
                <input
                  type="checkbox"
                  aria-label="Zaznacz wszystkie wnioski"
                  checked={allSelected}
                  onChange={() =>
                    setSelected(
                      allSelected
                        ? new Set()
                        : new Set(rows.map((row) => row.applicationId)),
                    )
                  }
                />
              </th>
              {[
                "Miejsce",
                "Numer",
                "Podmiot",
                "Tytuł projektu",
                "Ocena formalna",
                "Karty merytoryczne",
                "Punkty",
                "Strategiczne",
                "Razem",
                "Próg",
                "Ostrzeżenia",
                "Kwota wnioskowana",
                "Rekomendowana kwota",
                "Kwota przyznana",
                "Uwagi do decyzji",
                "Wynik",
                "Eksperci",
              ].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className={`${tableHeadCellClassName} text-left`}
                >
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr
                key={row.applicationId}
                // Below the threshold reads quieter: it is on the list for the
                // record, not for a decision.
                className={`hover:bg-surface-warm ${row.passesThreshold === false ? "text-text-muted" : ""}`}
              >
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  <input
                    type="checkbox"
                    aria-label={`Zaznacz wniosek ${row.number ?? ""}`}
                    checked={selected.has(row.applicationId)}
                    onChange={() => toggle(row.applicationId)}
                  />
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  <span className="font-semibold">{row.rank ?? ""}</span>
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  <Link
                    className="underline"
                    href={`${operatorPanelRoot}/evaluation/${competitionId}/${row.applicationId}`}
                  >
                    {row.number ?? "bez numeru"}
                  </Link>
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {row.entityName}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {row.projectTitle ?? ""}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  <StatusBadge tone={formalTones[row.formal]}>{formalLabels[row.formal]}</StatusBadge>
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {row.meritCardsFinished} z {row.meritCardsRequired}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {points(row.meritScore)}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {points(row.strategicScore)}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top text-base font-semibold tabular-nums">
                  {points(row.totalScore)}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {row.passesThreshold === null || row.passesThreshold === undefined ? null : (
                    <StatusBadge tone={row.passesThreshold ? "positive" : "negative"}>
                      {row.passesThreshold ? "powyżej" : "poniżej"}
                    </StatusBadge>
                  )}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {row.diverges ? <StatusBadge tone="attention">Rozbieżne oceny ekspertów</StatusBadge> : null}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top text-right">
                  {number(row.requestedGrant) === null
                    ? ""
                    : formatAmount(number(row.requestedGrant)!)}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top text-right">
                  {number(row.recommendedGrant) === null
                    ? ""
                    : formatAmount(number(row.recommendedGrant)!)}
                </td>
                {/* No key of its own: the list is read again after every
                    save, and new cells would drop the focus of someone
                    tabbing from the amount to the note. */}
                <DecisionCells
                  row={row}
                  locked={locked}
                  onSave={onDecide}
                />
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  {!row.status || row.status === "Submitted" ? null : (
                    <StatusBadge tone={applicationStatusTones[row.status]}>{applicationStatusLabels[row.status]}</StatusBadge>
                  )}
                </td>
                <td className="border-b border-border-muted px-3 py-2.5 align-top">
                  <AssignedExperts
                    applicationId={row.applicationId}
                    number={row.number ?? ""}
                    assigned={assignments
                      .filter(
                        (assignment) =>
                          assignment.applicationId === row.applicationId,
                      )
                      .map((assignment) => assignment.reviewerId)}
                    names={names}
                    reviewers={reviewers}
                    onAssign={(applicationId, reviewerId) =>
                      onAssign([applicationId], reviewerId)
                    }
                    onUnassign={onUnassign}
                  />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
