import type { Metadata } from "next";
import "./globals.css";

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
    <html lang="pl">
      <body className="min-h-screen antialiased">{children}</body>
    </html>
  );
}
