import type { Metadata } from "next";
import { headers } from "next/headers";
import { Playfair_Display, Poppins } from "next/font/google";
import "./globals.css";

import { contrastBootScript } from "@/lib/contrast-mode";

// latin-ext is required, not optional: the UI is Polish, and Polish diacritics
// (ą ć ę ł ń ó ś ź ż) live outside the plain latin subset.
const playfairDisplay = Playfair_Display({
  subsets: ["latin", "latin-ext"],
  weight: "800",
  variable: "--font-playfair-display",
});

const poppins = Poppins({
  subsets: ["latin", "latin-ext"],
  weight: ["400", "600"],
  variable: "--font-poppins",
});

export const metadata: Metadata = {
  title: "Generator konkursów OCWIP",
  description:
    "Platforma do ogłaszania konkursów dotacyjnych, składania i oceny wniosków oraz sprawozdawczości.",
};

export default async function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  // T-112: the nonce middleware.ts gave this request, so the policy lets
  // the boot script run. Reading it makes every page render per request.
  const nonce = (await headers()).get("x-nonce") ?? undefined;
  // lang="pl" is not cosmetic: it drives screen reader pronunciation and hyphenation.
  // suppressHydrationWarning: the boot script may set data-contrast on <html>
  // before React hydrates, which is the whole point of running it that early.
  return (
    <html
      lang="pl"
      className={`${playfairDisplay.variable} ${poppins.variable}`}
      suppressHydrationWarning
    >
      <head>
        {/* suppressHydrationWarning on the script itself, not only on <html>:
            the flag covers one element and its text, never the attributes of
            elements inside it. Chrome hides a nonce value from DOM reads once
            the policy is applied (the CSP nonce-hiding rule), so React reads
            "" on the client against the real value in the server HTML and logs
            a hydration mismatch on every page load. Nothing is broken, but a
            console that always has an error in it is a console nobody reads. */}
        <script
          nonce={nonce}
          suppressHydrationWarning
          dangerouslySetInnerHTML={{ __html: contrastBootScript }}
        />
      </head>
      <body className="min-h-screen antialiased">{children}</body>
    </html>
  );
}
