/**
 * Contracts (T-45): the template of a competition, and the contract of a
 * funded application, drawn up, filled in, printed and signed.
 */

import type { ApiPath } from "./api-client";
import { apiBaseUrl, apiFetch, apiFetchFile, fillPath } from "./api-client";
import type { components } from "./api-schema";

export type ContractTemplate = components["schemas"]["DocumentTemplateResponse"];
export type Contract = components["schemas"]["ContractResponse"];
export type ContractField = components["schemas"]["ContractField"];

/**
 * The placeholders the system fills itself, to show next to the template
 * editor. The same names as TemplatePlaceholders.SystemLabels in the backend;
 * any other name in a template is a blank the operator types in.
 */
export const systemPlaceholders: readonly { readonly name: string; readonly label: string }[] = [
  { name: "numer_umowy", label: "Numer umowy (numer wniosku)" },
  { name: "data_zawarcia", label: "Data zawarcia umowy słownie (data podpisania)" },
  { name: "numer_wniosku", label: "Numer wniosku" },
  { name: "data_zlozenia_wniosku", label: "Data złożenia wniosku słownie" },
  { name: "nazwa_realizatora", label: "Nazwa wnioskodawcy" },
  { name: "nip", label: "NIP wnioskodawcy" },
  { name: "adres", label: "Adres wnioskodawcy" },
  { name: "tytul_projektu", label: "Tytuł projektu" },
  { name: "koszt_calkowity", label: "Całkowity koszt projektu" },
  { name: "kwota_wnioskowana", label: "Kwota wnioskowana" },
  { name: "kwota_dotacji", label: "Kwota przyznanej dotacji" },
  { name: "kwota_dotacji_slownie", label: "Kwota przyznanej dotacji słownie" },
  { name: "numer_konkursu", label: "Numer konkursu" },
  { name: "tytul_konkursu", label: "Nazwa konkursu" },
  { name: "czlonkowie_grupy", label: "Członkowie grupy nieformalnej (z wniosku)" },
];

/**
 * Every contract of the competition at once (T-45b): the backend draws up the
 * missing ones and answers with a ZIP of the complete ones, plus braki.txt
 * naming those with a blank left.
 */
export async function bundleContracts(competitionId: string): Promise<{ blob: Blob; fileName: string | null }> {
  const template = "/competitions/{competitionId}/contracts/bundle" satisfies ApiPath;
  return apiFetchFile(fillPath(template, { competitionId }), { method: "POST" });
}

export async function fetchContractTemplate(competitionId: string): Promise<ContractTemplate> {
  const template = "/competitions/{competitionId}/contract-template" satisfies ApiPath;
  return apiFetch<ContractTemplate>(fillPath(template, { competitionId }), { cache: "no-store" });
}

export async function publishContractTemplate(competitionId: string, body: string): Promise<ContractTemplate> {
  const template = "/competitions/{competitionId}/contract-template" satisfies ApiPath;
  return apiFetch<ContractTemplate>(fillPath(template, { competitionId }), {
    method: "POST",
    body: JSON.stringify({ body }),
  });
}

export async function drawUpContract(applicationId: string): Promise<Contract> {
  const template = "/applications/{applicationId}/contract" satisfies ApiPath;
  return apiFetch<Contract>(fillPath(template, { applicationId }), { method: "POST" });
}

export async function fetchApplicationContract(applicationId: string): Promise<Contract> {
  const template = "/applications/{applicationId}/contract" satisfies ApiPath;
  return apiFetch<Contract>(fillPath(template, { applicationId }), { cache: "no-store" });
}

export async function saveContractValues(contractId: string, values: Record<string, string>): Promise<Contract> {
  const template = "/contracts/{contractId}/values" satisfies ApiPath;
  return apiFetch<Contract>(fillPath(template, { contractId }), {
    method: "PUT",
    body: JSON.stringify({ values }),
  });
}

export async function signContract(contractId: string, signedOn: string): Promise<Contract> {
  const template = "/contracts/{contractId}/sign" satisfies ApiPath;
  return apiFetch<Contract>(fillPath(template, { contractId }), {
    method: "POST",
    body: JSON.stringify({ signedOn }),
  });
}

/** A plain link: the browser downloads the file with the session cookie. */
export function contractPdfUrl(contractId: string): string {
  const template = "/contracts/{contractId}/pdf" satisfies ApiPath;
  return `${apiBaseUrl}${fillPath(template, { contractId })}`;
}
