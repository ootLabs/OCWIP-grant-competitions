import { EmptyState } from "./empty-state";

/**
 * A view named in the project docs that has no real implementation yet, but
 * already has its own task in docs/runbook/kolejka.md. Wired into routing and
 * navigation (T-122x) so the link that leads here is never a dead end, while
 * leaving the actual feature to that task: no logic, no data, no fetch.
 */
export function NotReadyView({ title }: { title: string }) {
  return (
    <section className="flex flex-col gap-4">
      <h1 className="text-2xl">{title}</h1>
      <EmptyState title="To jeszcze nie jest gotowe">
        Ten ekran pojawi się w jednej z kolejnych aktualizacji systemu.
      </EmptyState>
    </section>
  );
}
