/**
 * Polish wording for the enums a competition carries (T-23).
 *
 * Kept as full maps rather than functions with a default branch: a value added
 * to the API enum then stops compiling here, which is the moment to name it.
 * A default branch would instead print one wrong label forever, quietly, on
 * the page with the most outside traffic in the product.
 *
 * Names come from docs/slownik.md, not from the identifiers.
 */

import type {
  AllowedFileFormat,
  AttachmentRequirement,
  CostCategory,
} from "@/lib/competitions";
import type { components } from "@/lib/api-schema";
import type { StatusTone } from "@/lib/status-tone";

type CompetitionStatus = components["schemas"]["CompetitionStatus"];

/**
 * The stage of the competition, said the way the report says it.
 *
 * Draft is in the map although a draft never reaches a public page: leaving it
 * out would mean a partial map, and a partial map is the default branch this
 * file exists to avoid.
 */
export const statusLabels: Record<CompetitionStatus, string> = {
  Draft: "Roboczy",
  Published: "Ogłoszony",
  OpenForApplications: "Trwa nabór",
  Closed: "Nabór zamknięty",
  UnderReview: "Trwa ocena",
  Resolved: "Rozstrzygnięty",
  Archived: "Archiwalny",
};

/**
 * The colour of a competition's badge. Green only while somebody can apply,
 * since that is the one state a visitor acts on; announced is blue, the stages
 * where OCWIP is working are amber, and the finished ones are grey.
 */
export const statusTones: Record<CompetitionStatus, StatusTone> = {
  Draft: "neutral",
  Published: "info",
  OpenForApplications: "positive",
  Closed: "attention",
  UnderReview: "attention",
  Resolved: "info",
  Archived: "neutral",
};

export const attachmentRequirementLabels: Record<AttachmentRequirement, string> = {
  Required: "Wymagany",
  Optional: "Nieobowiązkowy",
  RequiredOutsideKrs: "Wymagany od podmiotów spoza KRS",
};

export const fileFormatLabels: Record<AllowedFileFormat, string> = {
  Pdf: "PDF",
  Doc: "DOC",
  Docx: "DOCX",
  Xls: "XLS",
  Xlsx: "XLSX",
  Jpg: "JPG",
  Odt: "ODT",
  Ods: "ODS",
};

export const costCategoryLabels: Record<CostCategory, string> = {
  DirectCosts: "Koszty bezpośrednie",
  InstitutionalDevelopment: "Rozwój instytucjonalny",
  IndirectCosts: "Koszty pośrednie",
};

/** What the percentage limits are counted from (step 1.4). */
export const percentageBasisLabels: Record<
  components["schemas"]["PercentageBasis"],
  string
> = {
  GrantAmount: "kwoty dotacji",
  TotalProjectValue: "całkowitej wartości projektu",
};
