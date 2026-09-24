"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { verifyEmailPath } from "@/lib/account";
import { ApiError } from "@/lib/api-client";
import {
  forgotPasswordPath,
  isRefusal,
  login,
  loginFailureMessage,
  registerPath,
  withReturnUrl,
} from "@/lib/login";

const inputClassName = "rounded-sm border border-border px-2 py-2";

/**
 * The sign in form (T-12.7).
 *
 * Where to go afterwards is the backend's answer (redirectPath), never
 * something this component works out, see lib/login.ts. replace rather than
 * push, so the back button does not return to a form that has done its job.
 */
export function LoginForm({ returnUrl }: { returnUrl: string | null }) {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [failure, setFailure] = useState<string | null>(null);
  // 403 is the one refusal with something to do about it: the address was
  // never confirmed, and the mail with the link may never have arrived.
  const [unconfirmed, setUnconfirmed] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting) {
      return;
    }

    setSubmitting(true);
    setFailure(null);
    setUnconfirmed(false);

    try {
      const session = await login(email, password, returnUrl);
      router.replace(session.redirectPath);
    } catch (error) {
      setFailure(loginFailureMessage(error));
      setUnconfirmed(error instanceof ApiError && error.status === 403);
      if (isRefusal(error)) {
        setPassword("");
      }
      setSubmitting(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <form className="flex flex-col gap-4" onSubmit={handleSubmit}>
        <label className="flex flex-col gap-1 text-sm">
          Adres e-mail
          <input
            autoComplete="email"
            className={inputClassName}
            name="email"
            onChange={(event) => setEmail(event.target.value)}
            required
            type="email"
            value={email}
          />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          Hasło
          <input
            autoComplete="current-password"
            className={inputClassName}
            name="password"
            onChange={(event) => setPassword(event.target.value)}
            required
            type="password"
            value={password}
          />
        </label>

        {failure !== null && (
          <p className="text-sm text-brand-accent-text" role="alert">
            {failure}
          </p>
        )}

        {unconfirmed && (
          <p className="text-sm">
            <Link href={withReturnUrl(verifyEmailPath, returnUrl)}>
              Wyślij link potwierdzający jeszcze raz
            </Link>
          </p>
        )}

        <button
          className="inline-flex w-full items-center justify-center rounded-sm bg-brand-accent px-5 py-3 text-bg hover:bg-brand-accent-hover disabled:opacity-70"
          disabled={submitting}
          type="submit"
        >
          {submitting ? "Trwa logowanie" : "Zaloguj się"}
        </button>
      </form>

      <ul className="flex flex-col gap-2 text-sm">
        <li>
          <Link href={forgotPasswordPath}>Nie pamiętam hasła</Link>
        </li>
        <li>
          Nie masz jeszcze konta?{" "}
          <Link href={withReturnUrl(registerPath, returnUrl)}>Załóż konto</Link>
        </li>
      </ul>
    </div>
  );
}
