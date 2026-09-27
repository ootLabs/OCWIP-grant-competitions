import type { Contract } from "./contracts";

/** One contract as the API sends it, for the tests of the contract screens. */
export function contractFixture(overrides: Partial<Contract> = {}): Contract {
  return {
    id: "k1",
    applicationId: "a1",
    applicationNumber: "1/2026/1",
    entityName: "Stowarzyszenie Łąka",
    templateVersion: 2,
    status: "Draft",
    signedOn: null,
    fields: [
      { name: "numer_umowy", label: "Numer umowy (numer wniosku)", system: true, value: "1/2026/1" },
      { name: "numer_rachunku", label: "Numer rachunku", system: false, value: null },
    ],
    ...overrides,
  } as Contract;
}
