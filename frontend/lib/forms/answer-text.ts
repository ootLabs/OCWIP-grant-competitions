/**
 * One answer as text to read, for a submitted offer shown to the operator
 * (T-35). The same formats the renderer puts into its inputs (lib/format.ts),
 * so an amount reads the same to the operator as it did to the applicant.
 * Null means nothing was answered; the caller says so in its own words.
 */
import type { FileAnswerValue, AnswerValue } from "./answer-types";
import type { FormField } from "./document-types";
import { formatAmount, formatDateOnly, formatMoment, formatPercent } from "../format";

function optionLabel(field: FormField, value: string): string {
  return field.options?.find((option) => option.value === value)?.label ?? value;
}

export function answerText(field: FormField, value: AnswerValue): string | null {
  if (value === null || value === undefined || value === "") {
    return null;
  }

  switch (field.type) {
    case "amount":
      return formatAmount(value as number | string);
    case "percent":
      return formatPercent(value as number | string);
    case "date":
      return formatDateOnly(String(value));
    case "dateTime":
      return formatMoment(String(value));
    case "yesNo":
      return value === true ? "Tak" : "Nie";
    case "statement":
      return value === true ? "Złożone" : "Niezłożone";
    case "singleChoice":
      return optionLabel(field, String(value));
    case "multipleChoice": {
      const values = value as readonly string[];
      return values.length === 0 ? null : values.map((v) => optionLabel(field, v)).join(", ");
    }
    case "file":
      return (value as FileAnswerValue).name;
    default:
      return String(value);
  }
}
