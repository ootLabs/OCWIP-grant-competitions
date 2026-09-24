import type { Metadata } from "next";

import { firstParam, type SearchParams } from "@/lib/search-params";

import { VerifyEmail } from "./verify-email";

export const metadata: Metadata = {
  title: "Potwierdzenie adresu e-mail | OCWIP",
};

/**
 * /verify-email (T-12.8), the page the link in the verification mail opens
 * (EmailVerificationService.cs builds it with userId, token and, when the
 * visitor came from a competition, returnUrl).
 */
export default async function VerifyEmailPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const { userId, token, returnUrl } = await searchParams;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Potwierdzenie adresu e-mail</h1>
      <VerifyEmail
        returnUrl={firstParam(returnUrl)}
        token={firstParam(token)}
        userId={firstParam(userId)}
      />
    </div>
  );
}
