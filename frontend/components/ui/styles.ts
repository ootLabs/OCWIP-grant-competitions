/**
 * The class strings every screen builds its controls and surfaces from.
 *
 * Strings rather than components, for the same reason statusActionClassName
 * was one (components/status-page.tsx): one look has to fit a next/link Link,
 * a plain <a> to a download on the API origin and a <button>, and a wrapper
 * component for each would be three components with one job.
 *
 * A disabled control keeps its resting look under the pointer
 * (disabled:hover:...): one that lights up on hover reads as pressable.
 *
 * Every action string ends in "ActionClassName". The source check in
 * app/accessibility-source.test.ts accepts a link styled with one of them,
 * because a filled or outlined box is a look that is not colour alone.
 */

const actionBase =
  "inline-flex min-h-11 items-center justify-center gap-2 rounded-sm border-2 px-5 py-2 text-sm font-semibold no-underline transition-colors disabled:cursor-not-allowed disabled:opacity-60";

/** The one thing a screen wants done: filled with the brand accent. */
export const primaryActionClassName = `${actionBase} border-brand-accent bg-brand-accent text-bg hover:border-brand-accent-hover hover:bg-brand-accent-hover hover:text-bg disabled:hover:border-brand-accent disabled:hover:bg-brand-accent`;

/** Everything else a person can press: outlined, filled on hover. */
export const secondaryActionClassName = `${actionBase} border-brand-accent bg-transparent text-brand-accent-text hover:bg-brand-accent hover:text-bg disabled:hover:bg-transparent disabled:hover:text-brand-accent-text`;

const compactBase =
  "inline-flex min-h-10 items-center justify-center gap-2 rounded-sm border border-brand-accent px-3 py-1.5 text-sm font-semibold no-underline disabled:cursor-not-allowed disabled:opacity-60";

/** A smaller secondary action, for a row in a table or a header. */
export const compactActionClassName = `${compactBase} text-brand-accent-text hover:bg-brand-accent hover:text-bg disabled:hover:bg-transparent disabled:hover:text-brand-accent-text`;

/** A smaller primary action: the way in from a header. */
export const compactPrimaryActionClassName = `${compactBase} bg-brand-accent text-bg hover:border-brand-accent-hover hover:bg-brand-accent-hover hover:text-bg disabled:hover:border-brand-accent disabled:hover:bg-brand-accent`;

/** A raised block on the page: a competition, an application, a group of facts. */
export const cardClassName = "rounded-lg border border-border bg-bg";

/** The warm variant, for the one block on a screen that is the point of it. */
export const highlightCardClassName = "rounded-lg border border-border bg-surface-warm";

/** Small caps line above a heading, naming what the heading belongs to. */
export const eyebrowClassName =
  "text-xs font-semibold uppercase tracking-wider text-brand-accent-text";

/**
 * A table that reads as one: grey heading row, rules between rows. The heading
 * cell leaves the alignment to the column (text-left or text-right), because
 * a figure column lines its heading up with its figures.
 */
export const tableClassName = "w-full border-collapse text-sm";
export const tableHeadCellClassName =
  "border-b-2 border-border bg-surface-muted px-3 py-2.5 text-xs font-semibold text-text-muted";
export const tableCellClassName = "border-b border-border-muted px-3 py-2.5 align-top";
