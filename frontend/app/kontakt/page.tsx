import type { Metadata } from "next";

import { PublicFrame } from "@/components/public-frame";
import { accessibilityStatement as statement } from "@/lib/accessibility-statement";

export const metadata: Metadata = {
  title: "Kontakt | Generator konkursów OCWIP",
};

/** Who runs the service and how to reach them (T-121), from the same data as the accessibility statement. */
export default function ContactPage() {
  return (
    <PublicFrame>
      <article className="flex flex-col gap-4">
        <h1 className="text-3xl">Kontakt</h1>
        <p>Serwis prowadzi {statement.entity}.</p>
        <address className="not-italic">
          {statement.entity}
          <br />
          {statement.address}
          <br />
          e-mail:{" "}
          <a className="underline" href={`mailto:${statement.email}`}>
            {statement.email}
          </a>
          <br />
          telefon: {statement.phone}
        </address>
        <p>
          W sprawie konkretnego konkursu pisz do osób wskazanych na jego stronie. Problemy z dostępnością serwisu
          zgłaszaj według deklaracji dostępności.
        </p>
      </article>
    </PublicFrame>
  );
}
