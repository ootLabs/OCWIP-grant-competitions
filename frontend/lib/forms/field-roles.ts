/**
 * Which roles a field may take in the creator (T-35), the same rules the
 * contract gate applies (backend Models/Forms/FormFieldRole.cs): the title
 * on a short text, the cost and the grant on an amount or on a calculated
 * field that is not a percentage, each role on at most one field.
 */
import { FIELD_ROLES, type FieldRole, type FormDocument, type FormField } from "./document-types";
import { allTopLevelFields } from "./evaluate";

export function roleFitsField(role: FieldRole, field: FormField): boolean {
  if (role === "projectTitle") {
    return field.type === "shortText";
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
