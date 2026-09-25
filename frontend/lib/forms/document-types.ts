/**
 * The form definition contract, on the frontend side (T-26).
 *
 * Mirrors docs/kontrakt-formularza.md and the backend's
 * backend/src/Ocwip.Api/Models/Forms/FormFieldType.cs field for field: same
 * wire names, same grouping into textual/numeric/choice/table kinds. The
 * backend reads this shape from an opaque JsonElement (T-24), so nothing
 * generates these types from the OpenAPI document; they are written by hand
 * and kept in sync on purpose, the same way the backend's own wire name table
 * is written by hand rather than derived from the enum.
 */

/** The fifteen kinds of field, in the order docs/runbook/pola.md lists them. */
export type FormFieldType =
  | "shortText"
  | "longText"
  | "number"
  | "amount"
  | "percent"
  | "date"
  | "dateTime"
  | "yesNo"
  | "singleChoice"
  | "multipleChoice"
  | "repeatableTable"
  | "fixedTable"
  | "file"
  | "statement"
  | "calculated";

export const ALL_FIELD_TYPES: readonly FormFieldType[] = [
  "shortText",
  "longText",
  "number",
  "amount",
  "percent",
  "date",
  "dateTime",
  "yesNo",
  "singleChoice",
  "multipleChoice",
  "repeatableTable",
  "fixedTable",
  "file",
  "statement",
  "calculated",
];

/** Kinds a table column may be, per FormFieldTypes.IsAllowedInTable. */
export const COLUMN_FIELD_TYPES: readonly FormFieldType[] = [
  "shortText",
  "longText",
  "number",
  "amount",
  "percent",
  "date",
  "dateTime",
  "yesNo",
  "singleChoice",
  "multipleChoice",
  "calculated",
];

export const TEXTUAL_TYPES: ReadonlySet<FormFieldType> = new Set([
  "shortText",
  "longText",
]);

export const NUMERIC_TYPES: ReadonlySet<FormFieldType> = new Set([
  "number",
  "amount",
  "percent",
  "calculated",
]);

export const CHOICE_TYPES: ReadonlySet<FormFieldType> = new Set([
  "singleChoice",
  "multipleChoice",
]);

export const TABLE_TYPES: ReadonlySet<FormFieldType> = new Set([
  "repeatableTable",
  "fixedTable",
]);

/** singleChoice, multipleChoice and yesNo, the only kinds visibleWhen can read. */
export const CONDITION_SOURCE_TYPES: ReadonlySet<FormFieldType> = new Set([
  "singleChoice",
  "multipleChoice",
  "yesNo",
]);

/**
 * What a field means outside the form (T-35, FormFieldRole.cs). Each role
 * may stand on at most one field of the document, never on a table column.
 */
export type FieldRole = "projectTitle" | "totalCost" | "requestedGrant";

export const FIELD_ROLES: readonly FieldRole[] = ["projectTitle", "totalCost", "requestedGrant"];

/**
 * The roles an evaluation card carries (T-38, FormFieldRole.cs). Kept apart
 * from FieldRole: the form creator offers FIELD_ROLES on an application form,
 * and the backend refuses these there.
 */
export type EvaluationRole = "formalCriterion" | "meritScore" | "strategicScore" | "recommendedGrant";

/**
 * A kind of applicant, spelled as EntityType travels over the API, which is
 * also how appliesTo spells it in a card (T-38, R-34).
 */
export type ApplicantKind = "InformalGroup" | "PatronInformalGroup" | "Organisation";

export interface FormOption {
  readonly value: string;
  readonly label: string;
}

export interface VisibleWhen {
  readonly field: string;
  readonly equalsAnyOf: readonly string[];
}

export type CalculationKind = "sum" | "product" | "ratio" | "difference";

export interface FormCalculation {
  readonly kind: CalculationKind;
  readonly operands: readonly string[];
}

export type LimitKind = "maxAmount" | "maxPercentOf";

export interface FormLimit {
  readonly kind: LimitKind;
  readonly basis: string;
  /** A percentage written into the form. Exactly one of this and percentFrom. */
  readonly percent?: number;
  /**
   * The competition setting the percentage comes from (T-31), such as
   * "competition.maxIndirectCostPercent": the threshold of a cost table is
   * the competition's to set, not the form's.
   */
  readonly percentFrom?: string;
}

export const ALLOWED_FILE_FORMATS = [
  "pdf",
  "doc",
  "docx",
  "xls",
  "xlsx",
  "jpg",
  "odt",
  "ods",
] as const;

export type AllowedFileFormat = (typeof ALLOWED_FILE_FORMATS)[number];

export interface FormFileRules {
  readonly allowedFormats: readonly AllowedFileFormat[];
  readonly maxSizeMegabytes: number;
}

export interface FormTableRow {
  readonly key: string;
  readonly label: string;
}

export interface FormTable {
  readonly columns: readonly FormField[];
  readonly minRows?: number;
  readonly maxRows?: number;
  readonly rows?: readonly FormTableRow[];
}

export interface FormField {
  readonly key: string;
  readonly type: FormFieldType;
  readonly label: string;
  readonly help: string;
  readonly required: boolean;
  /** D14: whether the field reaches the printed offer. */
  readonly printed: boolean;
  readonly maxLength?: number;
  readonly minLength?: number;
  readonly minValue?: number;
  readonly maxValue?: number;
  readonly visibleWhen?: VisibleWhen | null;
  readonly options?: readonly FormOption[];
  readonly table?: FormTable;
  readonly file?: FormFileRules;
  readonly statementText?: string;
  readonly calculation?: FormCalculation;
  readonly limits?: readonly FormLimit[];
  /** T-35: which column of the operator's list of applications this field fills; T-38: a card's roles. */
  readonly role?: FieldRole | EvaluationRole;
  /** T-38, evaluation cards only: the kinds of applicant this field is asked of. Absent means all. */
  readonly appliesTo?: readonly ApplicantKind[];
  /** T-38, evaluation cards only: what a "yes" is worth in a sum. */
  readonly points?: number;
}

export interface FormSection {
  readonly key: string;
  readonly title: string;
  readonly description: string;
  readonly visibleWhen?: VisibleWhen | null;
  readonly fields: readonly FormField[];
}

export interface FormDocument {
  readonly schemaVersion: 1;
  readonly sections: readonly FormSection[];
}

/**
 * The competition settings a limit may be measured against instead of a
 * field, with the prefix the contract requires on the wire.
 */
export const COMPETITION_LIMIT_BASES = [
  "maxGrantAmount",
  "minGrantAmount",
  "totalPoolAmount",
  "maxIndirectCostPercent",
  "maxInstitutionalDevelopmentPercent",
  "maxAverageAnnualRevenue",
] as const;

/**
 * The competition settings a percentage ceiling may take its percentage
 * from (FormLimit.percentFrom). Only thresholds, the same two the backend
 * accepts (FormSchemaReferences.PercentParameters).
 */
export const COMPETITION_PERCENT_SETTINGS = [
  "maxIndirectCostPercent",
  "maxInstitutionalDevelopmentPercent",
] as const;

const COMPETITION_BASIS_PREFIX = "competition.";

export function competitionBasis(
  setting: (typeof COMPETITION_LIMIT_BASES)[number],
): string {
  return `${COMPETITION_BASIS_PREFIX}${setting}`;
}

/** The competition setting a limit's basis names, or null when it names a field instead. */
export function competitionBasisSetting(basis: string): string | null {
  return basis.startsWith(COMPETITION_BASIS_PREFIX)
    ? basis.slice(COMPETITION_BASIS_PREFIX.length)
    : null;
}

/**
 * yesNo is stored as a boolean but compared, and written to `visibleWhen`,
 * as the literal strings "true"/"false" (docs/kontrakt-formularza.md). One
 * place for that conversion, used both by the answer-time engine
 * (evaluate.ts) and by the control that lets an applicant answer one
 * (components/form-renderer/choice-input.tsx).
 */
export function yesNoWireValue(value: boolean): "true" | "false" {
  return value ? "true" : "false";
}

export function yesNoFromWireValue(wireValue: string): boolean {
  return wireValue === "true";
}

export function cloneDocument(document: FormDocument): FormDocument {
  return JSON.parse(JSON.stringify(document)) as FormDocument;
}
