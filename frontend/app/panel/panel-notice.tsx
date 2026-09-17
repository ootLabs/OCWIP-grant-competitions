/**
 * The whole screen replaced by one sentence.
 *
 * Deliberately not a panel frame with a message inside it. A frame names the
 * signed in account and offers navigation, and neither is true while we do not
 * know who is here, or while we are telling somebody they do not belong here.
 */
export function PanelNotice({
  title,
  children,
  busy = false,
}: {
  title: string;
  children?: React.ReactNode;
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
      </div>
    </div>
  );
}
