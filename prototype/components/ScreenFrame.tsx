type ScreenFrameProps = {
  children: React.ReactNode;
  /** Width cap per screen, mirroring the artboard each one came from. */
  className?: string;
};

/**
 * The outline that makes a route read as one screen rather than as the page
 * itself. Kept separate from the screens so the prototype chrome can change
 * without touching nine files.
 */
export function ScreenFrame({ children, className = "max-w-4xl" }: ScreenFrameProps) {
  return (
    <div
      className={`overflow-hidden rounded-[var(--radius-sm)] border border-border bg-bg ${className}`}
    >
      {children}
    </div>
  );
}
