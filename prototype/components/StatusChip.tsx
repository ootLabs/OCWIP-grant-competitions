type StatusChipProps = {
  children: React.ReactNode;
  /**
   * accent: something is live or done and worth noticing.
   * muted: a neutral state, most of the list.
   * solid: something blocks the applicant and must not be missed.
   */
  variant?: "accent" | "muted" | "solid";
};

const VARIANTS = {
  accent: "border border-brand-accent text-brand-accent-text",
  muted: "border border-border bg-surface-muted text-text-link",
  solid: "bg-brand-accent text-bg",
} as const;

export function StatusChip({ children, variant = "muted" }: StatusChipProps) {
  return (
    <span
      className={`inline-flex shrink-0 items-center rounded-[var(--radius-pill)] px-3 py-1 text-xs font-semibold ${VARIANTS[variant]}`}
    >
      {children}
    </span>
  );
}
