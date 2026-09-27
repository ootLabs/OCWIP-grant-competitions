/**
 * The Podmiot's card (T-93, docs/runbook/pola.md step 2.2): filled in once, at
 * the first application, shown filled in on every application after that and
 * on "Mój profil". The backend checks every rule and the checksums; this file
 * only carries the data and the Polish words for it.
 */
import { ApiError, apiFetch } from "./api-client";
import type { components } from "./api-schema";
import { entityTypeLabels, type EntityType } from "./operator-applications";

export type EntityCard = components["schemas"]["EntityCardData"];
export type EntityCardResponse = components["schemas"]["EntityCardResponse"];
export type EntityRepresentative = components["schemas"]["EntityRepresentative"];
export type LegalForm = NonNullable<components["schemas"]["LegalForm"]>;
export type EntityRegister = NonNullable<components["schemas"]["EntityRegister"]>;

export { entityTypeLabels };

/** The order the choice is offered in: the common case first. */
export const entityTypeChoices: readonly EntityType[] = [
  "Organisation",
  "PatronInformalGroup",
  "InformalGroup",
];

/** What each choice means, under its label, so nobody has to guess which one they are. */
export const entityTypeHints: Record<EntityType, string> = {
  Organisation: "Stowarzyszenie, fundacja, klub sportowy, koło gospodyń wiejskich albo inna organizacja z wpisem do rejestru.",
  PatronInformalGroup:
    "Grupa bez osobowości prawnej, za którą wniosek składa organizacja-patron. Poniżej wpisujesz dane patrona.",
  InformalGroup: "Grupa bez osobowości prawnej i bez patrona. Podajesz tylko nazwę grupy, resztę we wniosku.",
};

export const legalFormLabels: Record<LegalForm, string> = {
  Association: "Stowarzyszenie",
  Foundation: "Fundacja",
  SportsClub: "Klub sportowy",
  RuralWomenCircle: "Koło gospodyń wiejskich",
  Other: "Inna",
};

export const registerLabels: Record<EntityRegister, string> = {
  Krs: "Krajowy Rejestr Sądowy (KRS)",
  Other: "Inny rejestr albo ewidencja",
};

/** An organisation card, and the patron's card of a group under patronage, is the full card. */
export function hasOrganisationCard(type: EntityType): boolean {
  return type !== "InformalGroup";
}

export function emptyCard(type: EntityType = "Organisation"): EntityCard {
  return {
    type,
    name: "",
    legalForm: null,
    legalFormOther: null,
    register: null,
    registerNumber: null,
    nip: null,
    regon: null,
    address: null,
    correspondenceAddress: null,
    phone: null,
    email: null,
    bankAccount: null,
    representatives: [{ firstName: "", lastName: "", function: "" }],
  };
}

/** The caller's own card, or null before the first application (404). */
export async function fetchMyEntityCard(): Promise<EntityCardResponse | null> {
  try {
    return await apiFetch<EntityCardResponse>("/me/entity", { cache: "no-store" });
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) {
      return null;
    }

    throw error;
  }
}

export async function saveMyEntityCard(card: EntityCard, exists: boolean): Promise<EntityCardResponse> {
  return apiFetch<EntityCardResponse>("/me/entity", {
    method: exists ? "PUT" : "POST",
    body: JSON.stringify(card),
  });
}
