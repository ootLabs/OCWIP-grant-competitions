import type { Metadata } from "next";

import { firstParam, type SearchParams } from "@/lib/search-params";

import { LoginForm } from "./login-form";

export const metadata: Metadata = {
  title: "Logowanie | OCWIP",
};

/**
 * /login (T-12.7), where the panel guard and "Wypełnij wniosek" send a
 * visitor without a session.
 *
 * The returnUrl is only ever passed through to the backend, which decides
 * whether it is safe (lib/login.ts).
 */
export default async function LoginPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const { returnUrl } = await searchParams;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Zaloguj się</h1>
      <LoginForm returnUrl={firstParam(returnUrl)} />
    </div>
  );
}
