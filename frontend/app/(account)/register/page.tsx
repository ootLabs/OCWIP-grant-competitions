import type { Metadata } from "next";

import { fetchConsents, unavailableMessage, type ConsentDocument } from "@/lib/account";
import { firstParam, type SearchParams } from "@/lib/search-params";

import { RegisterForm } from "./register-form";

export const metadata: Metadata = {
  title: "Zakładanie konta | Generator konkursów OCWIP",
};

/**
 * /register (T-12.8), reached from the sign in screen with the returnUrl the
 * visitor arrived with, which goes on into the verification mail. The terms
 * and the privacy notice are read here (T-107): without them there is no
 * form, because the backend refuses a registration that did not accept both.
 */
export default async function RegisterPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const { returnUrl } = await searchParams;

  let consents: ConsentDocument[] | null;
  try {
    consents = await fetchConsents();
  } catch {
    consents = null;
  }

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Załóż konto</h1>
      {consents === null ? (
        <p role="alert">{unavailableMessage}</p>
      ) : (
        <RegisterForm consents={consents} returnUrl={firstParam(returnUrl)} />
      )}
    </div>
  );
}
