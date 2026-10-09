/**
 * The kind of applicant in the form against the card the draft is filed for
 * (O-10 of the fourth GUI walkthrough). The server decides at submission
 * (ApplicantKinds.cs); these say the same thing before the applicant has
 * filled in fields that do not apply to them.
 */
import type { FormAnswers } from "./answer-types";
import type { ApplicantKind, FormDocument, FormField } from "./document-types";
import { fieldAnchorId } from "./field-anchor";
import type { SubmissionGap } from "./submission-gaps";

function kindField(document: FormDocument): { field: FormField; sectionKey: string; sectionTitle: string } | null {
  for (const section of document.sections) {
    const field = section.fields.find((candidate) => candidate.role === "applicantType");
    if (field) {
      return { field, sectionKey: section.key, sectionTitle: section.title };
    }
  }
  return null;
}

/** The same rule as ApplicantKinds.Fits on the server. */
function fits(card: ApplicantKind, kind: string): boolean {
  return card === "InformalGroup" ? kind === "InformalGroup" : kind === "Organisation" || kind === "PatronInformalGroup";
}

/**
 * An informal group without a patron can apply as nothing else, so its
 * answer is filled in for it. An organisation's card leaves two answers,
 * and picking one is the applicant's decision. Null when nothing changes.
 */
export function prefilledApplicantKind(
  document: FormDocument,
  answers: FormAnswers,
  card: ApplicantKind | null,
): FormAnswers | null {
  const found = kindField(document);
  if (card !== "InformalGroup" || found === null || answers[found.field.key] !== undefined) {
    return null;
  }
  return { ...answers, [found.field.key]: "InformalGroup" };
}

/** The answer that does not fit the card, as a gap next to the others, in the server's own words. */
export function applicantKindGap(
  document: FormDocument,
  answers: FormAnswers,
  card: ApplicantKind | null,
): SubmissionGap | null {
  const found = kindField(document);
  const answer = found ? answers[found.field.key] : undefined;
  if (card === null || found === null || typeof answer !== "string" || answer === "" || fits(card, answer)) {
    return null;
  }

  return {
    sectionKey: found.sectionKey,
    sectionTitle: found.sectionTitle,
    fieldKey: found.field.key,
    fieldLabel: found.field.label,
    message:
      card === "InformalGroup"
        ? 'Dane wnioskodawcy opisują grupę nieformalną bez patrona, więc wniosek składa się jako grupa nieformalna. Organizacja albo patron grupy potrzebuje karty organizacji w zakładce "Mój profil".'
        : "Dane wnioskodawcy opisują organizację, więc wniosek składa się jako organizacja albo jako patron grupy nieformalnej.",
    anchorId: fieldAnchorId(found.field.key),
  };
}
