/**
 * The Podmiot's card (T-93, docs/runbook/pola.md step 2.2): filled in once, at
 * the first application, shown filled in on every application after that and
 * on "Mój profil". One person may act for several cards and several people
 * for one (T-93a). The backend checks every rule and the checksums; this file
 * only carries the data and the Polish words for it.
 */
import { ApiError, apiFetch, fillPath, type ApiPath } from "./api-client";
import type { components } from "./api-schema";
import { entityTypeLabels, type EntityType } from "./operator-applications";

export type EntityCard = components["schemas"]["EntityCardData"];
export type EntityCardResponse = components["schemas"]["EntityCardResponse"];
export type EntityRepresentative = components["schemas"]["EntityRepresentative"];
export type LegalForm = NonNullable<components["schemas"]["LegalForm"]>;
export type EntityRegister = NonNullable<components["schemas"]["EntityRegister"]>;
export type EntityCardSummary = components["schemas"]["EntityCardSummary"];
export type EntityMember = components["schemas"]["EntityMemberResponse"];

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

/** Every card the caller acts for; empty before the first application. */
export async function fetchMyEntities(): Promise<EntityCardSummary[]> {
  return apiFetch<EntityCardSummary[]>("/me/entities" satisfies ApiPath, { cache: "no-store" });
}

export async function fetchEntityCard(id: string): Promise<EntityCardResponse> {
  const template = "/me/entities/{id}" satisfies ApiPath;
  return apiFetch<EntityCardResponse>(fillPath(template, { id }), { cache: "no-store" });
}

/** Founds a card when there is no id yet, corrects that card otherwise. */
export async function saveEntityCard(card: EntityCard, entityId: string | null): Promise<EntityCardResponse> {
  if (entityId === null) {
    return apiFetch<EntityCardResponse>("/me/entities" satisfies ApiPath, {
      method: "POST",
      body: JSON.stringify(card),
    });
  }

  const template = "/me/entities/{id}" satisfies ApiPath;
  return apiFetch<EntityCardResponse>(fillPath(template, { id: entityId }), {
    method: "PUT",
    body: JSON.stringify(card),
  });
}

/**
 * Founding refused because a card with this NIP exists (T-93a): the person
 * asks to join it instead.
 */
export function isNipTaken(error: unknown): boolean {
  return error instanceof ApiError && error.status === 409;
}
