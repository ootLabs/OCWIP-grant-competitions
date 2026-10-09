import type { Metadata } from "next";

import { firstParam, type SearchParams } from "@/lib/search-params";

import { ConfirmEmailChangeForm } from "./confirm-email-change-form";

export const metadata: Metadata = {
  title: "Nowy adres e-mail | Generator konkursów OCWIP",
};

/** /confirm-email-change (T-106), the page the link in the mail to the new address opens. */
export default async function ConfirmEmailChangePage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const { userId, token } = await searchParams;

  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Potwierdź nowy adres e-mail</h1>
      <ConfirmEmailChangeForm userId={firstParam(userId)} token={firstParam(token)} />
    </div>
  );
}
