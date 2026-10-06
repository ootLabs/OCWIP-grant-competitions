import type {
  CompetitionAttachment,
  CompetitionContact,
} from "@/lib/competitions";
import { templateDownloadUrl } from "@/lib/attachment-templates";

import { StatusBadge } from "@/components/ui/status-badge";
import { cardClassName } from "@/components/ui/styles";

import { attachmentRequirementLabels, fileFormatLabels } from "./labels";

/**
 * What to prepare and who to ask about it (T-23).
 *
 * Both lists are public on purpose. An applicant has to know what documents
 * they need before they decide whether to create an account, and the contacts
 * are work addresses of members of staff, published because the report asks
 * for exactly that. Nothing else off those accounts travels here.
 *
 * A requirement with a template (T-102) links to it: downloaded without
 * signing in, since the page itself is public.
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
          className={`${cardClassName} flex flex-col gap-1 px-5 py-4`}
          key={attachment.id}
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <p className="font-semibold">{attachment.title}</p>
            <StatusBadge tone={attachment.requirement === "Optional" ? "neutral" : "attention"}>
              {attachmentRequirementLabels[attachment.requirement]}
            </StatusBadge>
          </div>
          {attachment.allowedFormats.length === 0 ? null : (
            <p className="text-sm text-text-muted">
              Formaty: {attachment.allowedFormats.map((format) => fileFormatLabels[format]).join(", ")}
            </p>
          )}
          {attachment.description === null ? null : (
            <p className="mt-1 text-sm">{attachment.description}</p>
          )}
          {attachment.template ? (
            <p className="mt-1 text-sm">
              <a className="text-text-link underline" href={templateDownloadUrl(attachment.id)}>
                Pobierz wzór: {attachment.template.fileName}
              </a>
            </p>
          ) : null}
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
    <ul className="flex list-none flex-col gap-3">
      {contacts.map((contact) => (
        <li className="flex flex-col" key={contact.userId}>
          <span className="font-semibold">{contact.name}</span>
          <a className="break-all text-sm text-text-link underline" href={`mailto:${contact.email}`}>
            {contact.email}
          </a>
        </li>
      ))}
    </ul>
  );
}
