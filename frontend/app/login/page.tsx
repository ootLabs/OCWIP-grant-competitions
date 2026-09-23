import type { Metadata } from "next";

import { LoginForm } from "./login-form";

export const metadata: Metadata = {
  title: "Logowanie | OCWIP",
};

type PageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

/**
 * /login (T-12.7), where the panel guard and "Wypełnij wniosek" send a
 * visitor without a session.
 *
 * The returnUrl is read here, on the server, and handed down as a prop, rather
 * than by useSearchParams in the form: that hook would need a Suspense
 * boundary to render at all, and the value is only ever passed through to the
 * backend, which decides whether it is safe (lib/login.ts).
 */
export default async function LoginPage({ searchParams }: PageProps) {
  const { returnUrl } = await searchParams;

  // Two returnUrl parameters in one address arrive as an array. The first one
  // is what the link that brought the visitor here wrote.
  const proposed = Array.isArray(returnUrl) ? returnUrl[0] : returnUrl;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Zaloguj się</h1>
      <LoginForm returnUrl={proposed ?? null} />
    </div>
  );
}
