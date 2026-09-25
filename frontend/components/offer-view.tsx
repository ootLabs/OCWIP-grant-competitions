import type { AnswerValue, FormAnswers } from "@/lib/forms/answer-types";
import { resolveTableRows } from "@/lib/forms/answer-types";
import { answerText } from "@/lib/forms/answer-text";
import type { FormDocument, FormField } from "@/lib/forms/document-types";
import { TABLE_TYPES } from "@/lib/forms/document-types";
import {
  computeRowValue,
  computeTopLevelValue,
  isFieldVisible,
  isSectionVisible,
} from "@/lib/forms/evaluate";
import { formatComputedNumber } from "@/lib/forms/format-computed";

const empty = <span className="italic">brak odpowiedzi</span>;

/**
 * A submitted offer, read only (T-35): every section and field the
 * applicant saw, with what they answered, in the form version they filled
 * in. Hidden sections and fields are left out, the same rule the renderer
 * and the submission check follow: what is left in a hidden field from
 * before it was hidden is not part of the offer.
 *
 * Its own component rather than the renderer with its inputs disabled: a
 * disabled input is announced as "unavailable" and cuts long text at the
 * edge of the box, and neither is how an operator should read a document.
 */
export function OfferView({
  document,
  answers,
}: {
  document: FormDocument;
  answers: FormAnswers;
}) {
  return (
    <div className="flex flex-col gap-6">
      {document.sections
        .filter((section) => isSectionVisible(section, answers))
        .map((section) => (
          <section key={section.key} className="flex flex-col gap-3">
            <h2 className="text-xl">{section.title}</h2>
            <dl className="flex flex-col gap-3">
              {section.fields
                .filter((field) => isFieldVisible(field, answers))
                .map((field) => (
                  <div key={field.key} className="flex flex-col gap-1">
                    <dt className="text-sm font-semibold">{field.label}</dt>
                    <dd className="whitespace-pre-wrap break-words">
                      <FieldValue document={document} answers={answers} field={field} />
                    </dd>
                  </div>
                ))}
            </dl>
          </section>
        ))}
    </div>
  );
}

function FieldValue({
  document,
  answers,
  field,
}: {
  document: FormDocument;
  answers: FormAnswers;
  field: FormField;
}) {
  if (field.type === "calculated") {
    return formatComputedNumber(
      computeTopLevelValue(document, answers, field),
      field.calculation?.kind,
    );
  }

  if (TABLE_TYPES.has(field.type)) {
    return <TableValue answers={answers} field={field} />;
  }

  if (field.type === "statement") {
    return (
      <>
        <span className="block text-sm">{field.statementText}</span>
        {answerText(field, answers[field.key] as AnswerValue) ?? empty}
      </>
    );
  }

  return answerText(field, answers[field.key] as AnswerValue) ?? empty;
}

function TableValue({ answers, field }: { answers: FormAnswers; field: FormField }) {
  const rows = resolveTableRows(field, answers);
  const columns = field.table?.columns ?? [];
  const fixedRows = field.table?.rows;

  if (rows.length === 0) {
    return empty;
  }

  return (
    <div className="overflow-x-auto">
      <table className="min-w-full border-collapse text-sm">
        <thead>
          <tr className="border-b border-border">
            {fixedRows !== undefined ? <th scope="col" className="px-2 py-1" /> : null}
            {columns.map((column) => (
              <th key={column.key} scope="col" className="px-2 py-1 text-left font-normal">
                {column.label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={fixedRows?.[index]?.key ?? index} className="border-b border-border-muted">
              {fixedRows !== undefined ? (
                <th scope="row" className="px-2 py-1 text-left font-normal">
                  {fixedRows[index]?.label}
                </th>
              ) : null}
              {columns.map((column) => (
                <td key={column.key} className="px-2 py-1 align-top">
                  {column.type === "calculated"
                    ? formatComputedNumber(
                        computeRowValue(field, row, column.key),
                        column.calculation?.kind,
                      )
                    : (answerText(column, row[column.key]) ?? "")}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
