/**
 * The whole screen replaced by one sentence and a way out.
 *
 * Deliberately not a panel frame with a message inside it. A frame names the
 * signed in account and offers navigation, and neither is true while we do not
 * know who is here, or while we are telling somebody they do not belong here.
 *
 * Shared by the panel gate and by the error pages (app/error.tsx,
 * app/not-found.tsx) rather than copied: "session expired", "no access here"
 * and "this address does not exist" are the same screen with different words,
 * and one of them drifting away from the others is how a product starts
 * looking assembled from parts.
 */
export function StatusPage({
  title,
  children,
  actions,
  busy = false,
}: {
  title: string;
  children?: React.ReactNode;
  actions?: React.ReactNode;
  busy?: boolean;
}) {
  return (
    <div className="flex min-h-screen items-center justify-center px-4">
      <div
        className="max-w-md text-center"
        // Announced, because the state changes after the page has rendered and
        // a screen reader would otherwise never hear about it.
        aria-live="polite"
        aria-busy={busy || undefined}
      >
        <h1 className="text-2xl">{title}</h1>
        {children ? <p className="mt-3 text-sm">{children}</p> : null}
        {/* Every one of these screens is a dead end without this: there is no
            navigation here and nothing to wait for. */}
        {actions ? (
          <div className="mt-5 flex flex-wrap justify-center gap-3">{actions}</div>
        ) : null}
      </div>
    </div>
  );
}

/**
 * How anything inside `actions` looks.
 *
 * Exported rather than applied inside StatusPage, because the way back is a
 * link on one screen and a button on another (app/error.tsx retries in place),
 * and those cannot be styled from the outside without guessing at the markup.
 */
export const statusActionClassName =
  "rounded-sm border border-brand-accent px-4 py-2 text-sm text-brand-accent-text hover:bg-brand-accent hover:text-bg";
