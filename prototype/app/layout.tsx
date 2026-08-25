import type { Metadata } from "next";
import { Playfair_Display, Poppins } from "next/font/google";
import { ScreenNav } from "@/components/ScreenNav";
import "./globals.css";

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
  title: "Kierunek C, prototyp wizualny",
  description:
    "Wizualizacja kierunku C dla Generatora konkursów OCWIP: dziewięć ekranów ścieżki wnioskodawcy.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  // lang="pl" is not cosmetic: it drives screen reader pronunciation and hyphenation.
  return (
    <html lang="pl" className={`${playfairDisplay.variable} ${poppins.variable}`}>
      <body className="min-h-screen antialiased">
        <div className="mx-auto flex max-w-[1500px] flex-col gap-8 px-4 py-6 lg:flex-row lg:px-8">
          <aside className="shrink-0 lg:w-[248px]">
            <div className="flex flex-col gap-4 lg:sticky lg:top-6">
              <div className="flex flex-col gap-1">
                <span className="text-[11px] font-semibold uppercase tracking-[0.14em] text-brand-accent-text">
                  Prototyp wizualny
                </span>
                <span className="font-heading text-xl leading-tight">
                  Kierunek C, prowadzenie za rękę
                </span>
              </div>
              <ScreenNav />
              <p className="text-xs leading-relaxed text-text-link">
                Makieta wyglądu, nie działający system. Logika ogranicza się do tego, co
                potrzebne, żeby ekran dało się obejrzeć.
              </p>
            </div>
          </aside>

          <main className="min-w-0 grow">{children}</main>
        </div>
      </body>
    </html>
  );
}
