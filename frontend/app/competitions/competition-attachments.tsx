import type {
  CompetitionAttachment,
  CompetitionContact,
} from "@/lib/competitions";

import { attachmentRequirementLabels, fileFormatLabels } from "./labels";

/**
 * What to prepare and who to ask about it (T-23).
 *
 * Both lists are public on purpose. An applicant has to know what documents
 * they need before they decide whether to create an account, and the contacts
 * are work addresses of members of staff, published because the report asks
 * for exactly that. Nothing else off those accounts travels here.
 *
 * The file templates an operator attaches to each of these are NOT here: the
 * schema has no place for a file yet, storage is card T-32, and inventing a
 * download link for a file that does not exist is worse than not offering one.
 */
export function CompetitionAttachments({
  attachments,
}: {
  attachments: readonly CompetitionAttachment[];
}) {
  if (attachments.length === 0) {
    return (
      <p className="text-sm">
        Ten konkurs nie wymaga żadnych załączników poza samym wnioskiem.
      </p>
    );
  }

  return (
    <ul className="flex list-none flex-col gap-3">
      {attachments.map((attachment) => (
        <li
          className="rounded-sm border border-border-muted px-4 py-3"
          key={attachment.id}
        >
          <p className="font-semibold">{attachment.title}</p>
          <p className="text-sm">
            {attachmentRequirementLabels[attachment.requirement]}
            {attachment.allowedFormats.length === 0
              ? null
              : ` · formaty: ${attachment.allowedFormats
                  .map((format) => fileFormatLabels[format])
                  .join(", ")}`}
          </p>
          {attachment.description === null ? null : (
            <p className="mt-1 text-sm">{attachment.description}</p>
          )}
        </li>
      ))}
    </ul>
  );
}

export function CompetitionContacts({
  contacts,
}: {
  contacts: readonly CompetitionContact[];
}) {
  if (contacts.length === 0) {
    return (
      <p className="text-sm">
        Pytania o ten konkurs kieruj do biura OCWIP. Osoby prowadzące nabór
        pojawią się tutaj, gdy tylko zostaną wskazane.
      </p>
    );
  }

  return (
    <ul className="flex list-none flex-col gap-2">
      {contacts.map((contact) => (
        <li key={contact.userId}>
          {contact.name},{" "}
          <a className="text-text-link underline" href={`mailto:${contact.email}`}>
            {contact.email}
          </a>
        </li>
      ))}
    </ul>
  );
}
