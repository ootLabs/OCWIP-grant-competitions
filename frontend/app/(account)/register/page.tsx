import type { Metadata } from "next";

import { firstParam, type SearchParams } from "@/lib/search-params";

import { RegisterForm } from "./register-form";

export const metadata: Metadata = {
  title: "Zakładanie konta | OCWIP",
};

/**
 * /register (T-12.8), reached from the sign in screen with the returnUrl the
 * visitor arrived with, which goes on into the verification mail.
 */
export default async function RegisterPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const { returnUrl } = await searchParams;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Załóż konto</h1>
      <RegisterForm returnUrl={firstParam(returnUrl)} />
    </div>
  );
}
