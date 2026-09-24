import type { Metadata } from "next";

import { ForgotPasswordForm } from "./forgot-password-form";

export const metadata: Metadata = {
  title: "Reset hasła | OCWIP",
};

/** /forgot-password (T-12.8), linked from the sign in screen. */
export default function ForgotPasswordPage() {
  return (
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <h1>Nie pamiętam hasła</h1>
      <ForgotPasswordForm />
    </div>
  );
}
