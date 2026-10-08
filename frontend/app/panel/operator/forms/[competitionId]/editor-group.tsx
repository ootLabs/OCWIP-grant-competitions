/**
 * One labelled block of settings in the kreator's inspector: what the
 * applicant sees, what the system checks, when the field shows. Grouping by
 * what a setting does for the applicant, not by how it is stored, is what
 * lets a person who "nie zna się na technikaliach" (T-26) find it.
 */
export function EditorGroup({
  title,
  hint,
  children,
}: {
  title: string;
  hint?: string;
  children: React.ReactNode;
}) {
  return (
    <fieldset className="flex min-w-0 flex-col gap-3 rounded-md border border-border-muted p-4">
      <legend className="px-1 text-sm font-semibold">
        {title}
        {hint ? <span className="ml-2 font-normal text-text-muted">{hint}</span> : null}
      </legend>
      {children}
    </fieldset>
  );
}

export const inputClassName =
  "w-full rounded-sm border border-border-control bg-bg px-2.5 py-1.5 text-sm";
