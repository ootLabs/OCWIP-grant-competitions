"use client";

import Link from "next/link";
import { useState } from "react";

import { formatAmount } from "@/lib/format";
import { applicationStatusLabels } from "@/lib/operator-applications";
import {
  formalLabels,
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
      <div className="overflow-x-auto">
        <table className="w-full border-collapse text-sm">
          <caption className="sr-only">
            Lista rankingowa z postępem oceny i przypisanymi ekspertami
          </caption>
          <thead>
            <tr className="text-left">
              <th scope="col" className="border-b border-border px-2 py-1">
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
                  className="border-b border-border px-2 py-1"
                >
                  {heading}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.applicationId}>
                <td className="border-b border-border-muted px-2 py-1">
                  <input
                    type="checkbox"
                    aria-label={`Zaznacz wniosek ${row.number ?? ""}`}
                    checked={selected.has(row.applicationId)}
                    onChange={() => toggle(row.applicationId)}
                  />
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {row.rank ?? ""}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  <Link
                    className="underline"
                    href={`${operatorPanelRoot}/evaluation/${competitionId}/${row.applicationId}`}
                  >
                    {row.number ?? "bez numeru"}
                  </Link>
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {row.entityName}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {row.projectTitle ?? ""}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {formalLabels[row.formal]}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {row.meritCardsFinished} z {row.meritCardsRequired}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {points(row.meritScore)}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {points(row.strategicScore)}
                </td>
                <td className="border-b border-border-muted px-2 py-1 font-semibold">
                  {points(row.totalScore)}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {row.passesThreshold === null ||
                  row.passesThreshold === undefined
                    ? ""
                    : row.passesThreshold
                      ? "powyżej"
                      : "poniżej"}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
                  {row.diverges ? "Rozbieżne oceny ekspertów" : ""}
                </td>
                <td className="border-b border-border-muted px-2 py-1 text-right">
                  {number(row.requestedGrant) === null
                    ? ""
                    : formatAmount(number(row.requestedGrant)!)}
                </td>
                <td className="border-b border-border-muted px-2 py-1 text-right">
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
                <td className="border-b border-border-muted px-2 py-1">
                  {!row.status || row.status === "Submitted" ? "" : applicationStatusLabels[row.status]}
                </td>
                <td className="border-b border-border-muted px-2 py-1">
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
