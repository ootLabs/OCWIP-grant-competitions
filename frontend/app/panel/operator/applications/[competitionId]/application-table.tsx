"use client";

import Link from "next/link";
import { useState } from "react";

import { StatusBadge } from "@/components/ui/status-badge";
import {
  cardClassName,
  tableCellClassName,
  tableClassName,
  tableHeadCellClassName,
} from "@/components/ui/styles";
import { formatAmount, formatMoment } from "@/lib/format";
import { formalLabels, formalTones } from "@/lib/operator-evaluation";
import {
  applicationStatusLabels,
  applicationStatusTones,
  defaultListView,
  entityTypeLabels,
  requestedSum,
  visibleRows,
  type ApplicationList,
  type ApplicationStatus,
  type EntityType,
  type ListView,
  type SortKey,
} from "@/lib/operator-applications";

interface Column {
  readonly key: SortKey | null;
  readonly label: string;
  /** Fixed widths, so the table keeps its shape while rows arrive or re-sort. */
  readonly width: string;
  readonly numeric?: boolean;
}

const columns: readonly Column[] = [
  { key: null, label: "Lp.", width: "w-12", numeric: true },
  { key: "number", label: "Numer wniosku", width: "w-24" },
  { key: "entityName", label: "Nazwa podmiotu", width: "w-56" },
  { key: "entityType", label: "Rodzaj wnioskodawcy", width: "w-40" },
  { key: "projectTitle", label: "Tytuł projektu", width: "w-72" },
  { key: "totalCost", label: "Całkowity koszt zadania", width: "w-40", numeric: true },
  { key: "requestedGrant", label: "Wnioskowana kwota", width: "w-40", numeric: true },
  { key: "status", label: "Status", width: "w-56" },
  { key: "formal", label: "Ocena formalna", width: "w-36" },
  { key: "submittedAt", label: "Data złożenia", width: "w-44" },
];

function amount(value: number | string | null): string {
  return value === null ? "brak" : formatAmount(value);
}

/**
 * The list itself (T-35): sortable by every column but the ordinal, filtered
 * by status and by the kind of applicant, with the requested total and what
 * is left of the pool at the bottom. The ordinal counts the rows as they are
 * shown, the way a printed list numbers them.
 */
export function ApplicationTable({ list }: { list: ApplicationList }) {
  const [view, setView] = useState<ListView>(defaultListView);
  const rows = visibleRows(list.applications, view);
  const filtered = rows.length !== list.applications.length;
  const statuses = [...new Set(list.applications.map((item) => item.status))];
  const entityTypes = [...new Set(list.applications.map((item) => item.entityType))];

  function sortBy(key: SortKey) {
    setView((current) => ({
      ...current,
      sortKey: key,
      descending: current.sortKey === key ? !current.descending : false,
    }));
  }

  return (
    <div className={`${cardClassName} flex flex-col`}>
      <div className="flex flex-wrap items-end gap-4 border-b border-border-muted px-4 py-4">
        <label className="flex flex-col gap-1 text-sm">
          Status
          <select
            className="min-h-10 rounded-sm border border-border-control bg-bg px-3"
            value={view.status}
            onChange={(event) =>
              setView({ ...view, status: event.target.value as ApplicationStatus | "all" })
            }
          >
            <option value="all">Wszystkie</option>
            {statuses.map((status) => (
              <option key={status} value={status}>
                {applicationStatusLabels[status]}
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          Rodzaj wnioskodawcy
          <select
            className="min-h-10 rounded-sm border border-border-control bg-bg px-3"
            value={view.entityType}
            onChange={(event) =>
              setView({ ...view, entityType: event.target.value as EntityType | "all" })
            }
          >
            <option value="all">Wszystkie</option>
            {entityTypes.map((type) => (
              <option key={type} value={type}>
                {entityTypeLabels[type]}
              </option>
            ))}
          </select>
        </label>
        <p className="ml-auto text-sm text-text-muted" aria-live="polite">
          {filtered
            ? `Widać ${rows.length} z ${list.applications.length} wniosków.`
            : `Wniosków: ${list.applications.length}.`}
        </p>
      </div>

      {/* Scrolls inside the card, not the page: see operator-panel.tsx.
          relative keeps anything absolutely positioned inside it in the box. */}
      <div className="relative overflow-x-auto">
      <table className={`${tableClassName} min-w-[82rem] table-fixed`}>
        <caption className="sr-only">
          Złożone wnioski w konkursie {list.competitionNumber}
        </caption>
        <colgroup>
          {columns.map((column) => (
            <col key={column.label} className={column.width} />
          ))}
        </colgroup>
        <thead>
          <tr>
            {columns.map((column) => (
              <th
                key={column.label}
                scope="col"
                className={`${tableHeadCellClassName} ${column.numeric ? "text-right" : "text-left"}`}
                aria-sort={
                  column.key !== null && column.key === view.sortKey
                    ? view.descending
                      ? "descending"
                      : "ascending"
                    : undefined
                }
              >
                {column.key === null ? (
                  column.label
                ) : (
                  <button
                    type="button"
                    className="whitespace-normal text-left underline decoration-dotted"
                    onClick={() => sortBy(column.key!)}
                  >
                    {column.label}
                    {column.key === view.sortKey ? (view.descending ? " ▼" : " ▲") : null}
                  </button>
                )}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((item, index) => (
            <tr key={item.id} className="hover:bg-surface-warm">
              <td className={`${tableCellClassName} text-right tabular-nums text-text-muted`}>{index + 1}</td>
              <td className={tableCellClassName}>
                <Link
                  href={`/panel/operator/applications/${list.competitionId}/${item.id}`}
                  className="font-semibold underline"
                >
                  {item.number}
                </Link>
              </td>
              <td className={`${tableCellClassName} break-words`}>{item.entityName}</td>
              <td className={`${tableCellClassName} text-text-muted`}>{entityTypeLabels[item.entityType]}</td>
              <td className={`${tableCellClassName} break-words`}>{item.projectTitle ?? "brak"}</td>
              <td className={`${tableCellClassName} text-right tabular-nums`}>{amount(item.totalCost)}</td>
              <td className={`${tableCellClassName} text-right font-semibold tabular-nums`}>{amount(item.requestedGrant)}</td>
              <td className={tableCellClassName}>
                <StatusBadge tone={applicationStatusTones[item.status]}>{applicationStatusLabels[item.status]}</StatusBadge>
              </td>
              <td className={tableCellClassName}>
                <StatusBadge tone={formalTones[item.formal]}>{formalLabels[item.formal]}</StatusBadge>
              </td>
              <td className={`${tableCellClassName} tabular-nums`}>{formatMoment(item.submittedAt)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot>
          <tr className="border-t-2 border-border bg-surface-muted">
            <th scope="row" colSpan={6} className="px-3 py-2.5 text-right font-normal">
              {filtered ? "Suma wnioskowanych kwot (widoczne wiersze)" : "Suma wnioskowanych kwot"}
            </th>
            <td className="px-3 py-2.5 text-right font-semibold tabular-nums">
              {formatAmount(filtered ? requestedSum(rows) : list.requestedTotal)}
            </td>
            <td colSpan={3} />
          </tr>
          <tr className="bg-surface-muted">
            <th scope="row" colSpan={6} className="px-3 py-2.5 text-right font-normal">
              Pozostało z puli konkursu
            </th>
            <td className="px-3 py-2.5 text-right font-semibold tabular-nums">
              {list.poolRemaining === null ? "pula nieustawiona" : formatAmount(list.poolRemaining)}
            </td>
            <td colSpan={3} className="px-3 py-2.5 text-text-muted">
              {list.totalPoolAmount === null ? null : `z ${formatAmount(list.totalPoolAmount)}`}
            </td>
          </tr>
        </tfoot>
      </table>
      </div>
    </div>
  );
}
