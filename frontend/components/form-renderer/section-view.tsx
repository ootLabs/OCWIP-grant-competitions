"use client";

import { TABLE_TYPES } from "@/lib/forms/document-types";
import type { FormSection } from "@/lib/forms/document-types";
import { FieldView } from "./field-view";
import { TableField } from "./table-field";

/**
 * One section's worth of fields, one column, read top to bottom (proces.md
 * rule 5). A table field skips the common label/help/error wrapper of
 * field-view.tsx entirely, because a table's rows are its own layout.
 */
export function SectionView({ section }: { section: FormSection }) {
  return (
    <div className="flex flex-col gap-6">
      {section.description ? <p className="text-sm">{section.description}</p> : null}
      {section.fields.map((field) =>
        TABLE_TYPES.has(field.type) ? (
          <TableField key={field.key} field={field} />
        ) : (
          <FieldView key={field.key} field={field} />
        ),
      )}
    </div>
  );
}
