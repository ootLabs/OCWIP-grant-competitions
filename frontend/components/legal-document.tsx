import { PublicFrame } from "@/components/public-frame";
import { fetchConsents, unavailableMessage, type ConsentDocument } from "@/lib/account";

/**
 * A legal text as a page of its own (T-121): the same document a person
 * accepts at registration (backend/seed/consents, T-107), read from the
 * same /public/consents, so the page and the consent never differ. Its
 * first line ("# ...") is the heading, the rest paragraphs as written.
 */
export async function LegalDocumentPage({ kind }: { kind: "terms" | "privacy" }) {
  let document: ConsentDocument | undefined;
  try {
    document = (await fetchConsents()).find((item) => item.kind === kind);
  } catch {
    document = undefined;
  }

  if (!document) {
    return (
      <PublicFrame>
        <p role="alert">{unavailableMessage}</p>
      </PublicFrame>
    );
  }

  const [, ...rest] = document.text.split("\n");
  const paragraphs = rest.join("\n").split(/\n\s*\n/).map((part) => part.trim()).filter((part) => part.length > 0);

  return (
    <PublicFrame>
      <article className="flex flex-col gap-4">
        <h1 className="text-3xl">{document.title}</h1>
        {paragraphs.map((paragraph, index) => (
          <p key={index} className="whitespace-pre-line">
            {paragraph}
          </p>
        ))}
      </article>
    </PublicFrame>
  );
}
