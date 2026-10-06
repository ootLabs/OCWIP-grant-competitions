/**
 * The state of something, as a small labelled pill.
 *
 * The words carry the meaning; the colour only groups them. That is not a
 * courtesy: the high contrast palette paints every tone the same yellow on
 * black (app/globals.css), so a badge whose text did not say what it means
 * would mean nothing there.
 */
import type { StatusTone } from "@/lib/status-tone";

export type { StatusTone };

const toneClassNames: Record<StatusTone, string> = {
  positive: "bg-status-positive-bg text-status-positive-text",
  attention: "bg-status-attention-bg text-status-attention-text",
  neutral: "bg-status-neutral-bg text-status-neutral-text",
  info: "bg-status-info-bg text-status-info-text",
  negative: "bg-status-negative-bg text-status-negative-text",
};

export function StatusBadge({
  tone,
  children,
}: {
  tone: StatusTone;
  children: React.ReactNode;
}) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 whitespace-nowrap rounded-pill border border-status-border px-2.5 py-0.5 text-xs font-semibold leading-5 ${toneClassNames[tone]}`}
    >
      <span aria-hidden="true" className="size-1.5 rounded-full bg-current" />
      {children}
    </span>
  );
}
