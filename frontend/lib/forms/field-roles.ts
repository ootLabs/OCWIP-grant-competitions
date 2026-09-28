/**
 * Which roles a field may take in the creator (T-35), the same rules the
 * contract gate applies (backend Models/Forms/FormFieldRole.cs): the title
 * on a short text, the cost and the grant on an amount or on a calculated
 * field that is not a percentage, the kind of applicant on a single choice
 * named after the kinds (T-94), the group's members on a table with a short
 * text column for the names (T-45b), each role on at most one field.
 */
import {
  APPLICANT_KIND_VALUES,
  FIELD_ROLES,
  type FieldRole,
  type FormDocument,
  type FormField,
} from "./document-types";
import { allTopLevelFields } from "./evaluate";

export function roleFitsField(role: FieldRole, field: FormField): boolean {
  if (role === "projectTitle") {
    return field.type === "shortText";
  }
  // T-94: a single choice whose every option is named after a kind of
  // applicant, so the answer is the kind itself.
  if (role === "applicantType") {
    const options = field.options ?? [];
    return (
      field.type === "singleChoice" &&
      options.length > 0 &&
      options.every((option) => APPLICANT_KIND_VALUES.includes(option.value))
    );
  }
  // T-45b: the contract lists the first short text column of each row.
  if (role === "groupMembers") {
    return (
      (field.type === "repeatableTable" || field.type === "fixedTable") &&
      (field.table?.columns ?? []).some((column) => column.type === "shortText")
    );
  }
  return (
    field.type === "amount" ||
    (field.type === "calculated" && field.calculation?.kind !== "ratio")
  );
}

/** The roles this field could take, each with the field that already holds it, if any. */
export function roleChoices(
  document: FormDocument,
  field: FormField,
): { role: FieldRole; takenBy: FormField | null }[] {
  const others = allTopLevelFields(document).filter((other) => other.key !== field.key);

  return FIELD_ROLES.filter((role) => roleFitsField(role, field)).map((role) => ({
    role,
    takenBy: others.find((other) => other.role === role) ?? null,
  }));
}
