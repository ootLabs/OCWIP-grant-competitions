/**
 * The shape of a panel, drawn while the gate is still asking who is here.
 *
 * Not a centred "loading" message, which is what used to stand here: that
 * message and the frame that replaces it are two different layouts, so the
 * header, the navigation and the first row of content all jumped into place a
 * moment after the answer arrived. The operator opens lists of over a hundred
 * applications, and a layout that moves under a cursor turns a click on one row
 * into a click on another (card T-15.4).
 *
 * The geometry is not guessed at: the same paddings as the real header, the
 * row width the panel actually uses, and one placeholder per real navigation
 * link, counted from that panel's navigation module. A skeleton that is close
 * enough is a skeleton that still moves the page.
 *
 * Nothing here says who is signed in, and the mode band is a blank strip rather
 * than the words "Tryb operatora". At this point GET /me has not answered, so
 * every one of those would be a guess about somebody else's data.
 */
export function PanelSkeleton({
  links,
  rowClassName = "",
  modeBar = false,
}: {
  /** How many placeholders the navigation gets, so the header is as tall as it will be. */
  links: number;
  /** How the panel constrains a row, applied exactly as the real frame applies it. */
  rowClassName?: string;
  /** The operator's mode band, reserved blank. */
  modeBar?: boolean;
}) {
  return (
    <div className="flex min-h-screen flex-col">
      {/* One sentence for a screen reader, which has nothing to read in a
          picture of a panel. The blocks below are decoration and are hidden
          from it entirely. */}
      <p className="sr-only" role="status">
        Sprawdzamy sesję i wczytujemy panel...
      </p>

      <div aria-hidden="true" className="flex min-h-screen flex-col motion-safe:animate-pulse">
        <header className="border-b border-border">
          {modeBar ? <div className="h-8 bg-surface-muted" /> : null}

          <div className={`flex w-full items-center gap-3 px-4 py-3 sm:px-6 ${rowClassName}`}>
            <div className="h-9 w-32 rounded-sm bg-surface-muted" />
            <div className="ml-auto h-5 w-40 rounded-sm bg-surface-muted" />
          </div>

          <div className={`flex w-full gap-1 border-t border-border-muted px-2 sm:px-4 ${rowClassName}`}>
            {Array.from({ length: links }, (_, item) => (
              <div key={item} className="px-3 py-3">
                <div className="h-5 w-24 rounded-sm bg-surface-muted" />
              </div>
            ))}
          </div>
        </header>

        <main className={`w-full flex-1 px-4 py-6 sm:px-6 ${rowClassName}`}>
          <div className="h-8 w-56 rounded-sm bg-surface-muted" />
          <div className="mt-4 h-40 rounded-sm bg-surface-muted" />
        </main>
      </div>
    </div>
  );
}
