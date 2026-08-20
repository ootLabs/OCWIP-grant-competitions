import type { Metadata } from "next";
import { Playfair_Display, Poppins } from "next/font/google";
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
  title: "Generator konkursów OCWIP",
  description:
    "Platforma do ogłaszania konkursów dotacyjnych, składania i oceny wniosków oraz sprawozdawczości.",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  // lang="pl" is not cosmetic: it drives screen reader pronunciation and hyphenation.
  return (
    <html lang="pl" className={`${playfairDisplay.variable} ${poppins.variable}`}>
      <body className="min-h-screen antialiased">{children}</body>
    </html>
  );
}
