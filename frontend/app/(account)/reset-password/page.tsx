import type { Metadata } from "next";

import { firstParam, type SearchParams } from "@/lib/search-params";

import { ResetPasswordForm } from "./reset-password-form";

export const metadata: Metadata = {
  title: "Nowe hasło | OCWIP",
};

/**
 * /reset-password (T-12.8), the page the link in the reset mail opens
 * (PasswordResetService.cs builds it with userId and token).
 */
export default async function ResetPasswordPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const { userId, token } = await searchParams;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Ustaw nowe hasło</h1>
      <ResetPasswordForm token={firstParam(token)} userId={firstParam(userId)} />
    </div>
  );
}
