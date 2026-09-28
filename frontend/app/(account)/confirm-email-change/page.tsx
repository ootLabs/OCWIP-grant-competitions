import type { Metadata } from "next";

import { firstParam, type SearchParams } from "@/lib/search-params";

import { ConfirmEmailChangeForm } from "./confirm-email-change-form";

export const metadata: Metadata = {
  title: "Nowy adres e-mail | OCWIP",
};

/** /confirm-email-change (T-106), the page the link in the mail to the new address opens. */
export default async function ConfirmEmailChangePage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const { userId, email, token } = await searchParams;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Potwierdź nowy adres e-mail</h1>
      <ConfirmEmailChangeForm userId={firstParam(userId)} email={firstParam(email)} token={firstParam(token)} />
    </div>
  );
}
