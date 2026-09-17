import Link from "next/link";

import { statusActionClassName } from "./status-page";

/**
 * What a list says when it holds nothing.
 *
 * The client will see the first competition on a system that is empty in every
 * direction: no competitions, no applications, no reviewers (card T-15.4). A
 * screen that answers that with blank space reads as broken, so every empty
 * list here says what is missing and what to do about it.
 *
 * The next step is a sentence, not only a button: most of these steps are not
 * one click away (an applicant waits for a call for proposals to open), and a
 * button that does nothing is worse than a sentence that explains.
 */
export function EmptyState({
  title,
  children,
  action,
}: {
  title: string;
  children: React.ReactNode;
  action?: { readonly href: string; readonly label: string };
}) {
  return (
    <div className="rounded-sm border border-dashed border-border bg-surface-muted px-4 py-8 text-center sm:px-6">
      <h2 className="text-lg">{title}</h2>
      <p className="mx-auto mt-2 max-w-prose text-sm">{children}</p>
      {action ? (
        <p className="mt-5">
          <Link className={statusActionClassName} href={action.href}>
            {action.label}
          </Link>
        </p>
      ) : null}
    </div>
  );
}
