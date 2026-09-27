"use client";

import { useEffect, useState } from "react";

import { statusActionClassName } from "@/components/status-page";
import { contractPdfUrl, fetchApplicationContract, type Contract } from "@/lib/contracts";
import { formatDateOnly } from "@/lib/format";

/**
 * The applicant's contract (T-45), once the operator has drawn it up: to
 * read and print before signing, and the day it was signed afterwards.
 * Nothing at all before that: there is no contract to talk about.
 */
export function ContractEntry({ applicationId }: { applicationId: string }) {
  const [contract, setContract] = useState<Contract | null>(null);

  useEffect(() => {
    let current = true;
    fetchApplicationContract(applicationId)
      .then((loaded) => {
        if (current) setContract(loaded);
      })
      // 404 is the normal "not drawn up yet"; any other failure leaves the
      // section out as well, and the rest of the page still works.
      .catch(() => undefined);
    return () => {
      current = false;
    };
  }, [applicationId]);

  if (contract === null) {
    return null;
  }

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-xl">Umowa</h2>
      <p className="text-sm">
        {contract.status === "Signed" && contract.signedOn
          ? `Umowa podpisana ${formatDateOnly(contract.signedOn)}.`
          : "Umowa jest przygotowana do podpisu. Przeczytaj ją przed spotkaniem w OCWIP."}
      </p>
      <p>
        <a href={contractPdfUrl(contract.id)} className={statusActionClassName}>
          Pobierz umowę (PDF)
        </a>
      </p>
    </section>
  );
}
