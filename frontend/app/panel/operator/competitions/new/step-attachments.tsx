"use client";

import type { AttachmentDraft, CompetitionDraft } from "@/lib/competition-wizard/types";

import { AttachmentRow } from "./attachment-row";
import { FieldError } from "./field-error";

const EMPTY_ATTACHMENT: AttachmentDraft = {
  title: "",
  description: "",
  requirement: "Required",
  allowedFormats: [],
};

/**
 * Krok 1.5: Załączniki do oferty (docs/runbook/pola.md). Wzór pliku do
 * pobrania świadomie nie tu: przechowywanie plików to T-32, tak jak w
 * odpowiedniku publicznym (competition-attachments.tsx).
 */
export function StepAttachments({
  draft,
  onChange,
  fieldErrors,
}: {
  draft: CompetitionDraft;
  onChange: (patch: Partial<CompetitionDraft>) => void;
  fieldErrors: Record<string, string[]>;
}) {
  const setAttachment = (index: number, attachment: AttachmentDraft) => {
    onChange({
      attachments: draft.attachments.map((a, i) =>
        i === index ? attachment : a,
      ),
    });
  };

  const removeAttachment = (index: number) => {
    onChange({
      attachments: draft.attachments.filter((_, i) => i !== index),
    });
  };

  const addAttachment = () => {
    onChange({ attachments: [...draft.attachments, EMPTY_ATTACHMENT] });
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-4">
        <label className="flex flex-col gap-1 text-sm">
          Limit rozmiaru pojedynczego pliku (MB)
          <input
            type="number"
            min={1}
            className="w-24 rounded-sm border border-border-control px-2 py-1"
            value={draft.maxAttachmentSizeInMegabytes}
            onChange={(event) =>
              onChange({ maxAttachmentSizeInMegabytes: event.target.value })
            }
          />
          <FieldError messages={fieldErrors.maxAttachmentSizeInBytes} />
        </label>

        <label className="flex flex-col gap-1 text-sm">
          Limit rozmiaru całego wniosku (MB)
          <input
            type="number"
            min={1}
            className="w-24 rounded-sm border border-border-control px-2 py-1"
            value={draft.maxApplicationSizeInMegabytes}
            onChange={(event) =>
              onChange({ maxApplicationSizeInMegabytes: event.target.value })
            }
          />
          <FieldError messages={fieldErrors.maxApplicationSizeInBytes} />
        </label>
      </div>

      <FieldError messages={fieldErrors.attachments} />

      {draft.attachments.length === 0 ? (
        <p className="text-sm">
          Konkurs nie wymaga jeszcze żadnych załączników poza samym wnioskiem.
        </p>
      ) : (
        <div className="flex flex-col gap-3">
          {draft.attachments.map((attachment, index) => (
            <AttachmentRow
              // The list has no id of its own (CompetitionAttachmentRequest,
              // by design): position is what both sides key it by.
              key={index}
              groupName={`attachment-requirement-${index}`}
              attachment={attachment}
              fieldErrors={extractIndexedErrors(fieldErrors, index)}
              onChange={(next) => setAttachment(index, next)}
              onRemove={() => removeAttachment(index)}
            />
          ))}
        </div>
      )}

      <button
        type="button"
        className="self-start rounded-sm border border-border px-3 py-1.5 text-sm"
        onClick={addAttachment}
      >
        Dodaj załącznik
      </button>
    </div>
  );
}

/**
 * CompetitionRequestValidator writes "attachments[3].title", indexed; this
 * pulls out the slice for one row so AttachmentRow can look fields up by
 * their plain name, the same way every other step does.
 */
function extractIndexedErrors(
  fieldErrors: Record<string, string[]>,
  index: number,
): Record<string, string[]> {
  const prefix = `attachments[${index}].`;
  const result: Record<string, string[]> = {};

  for (const [key, messages] of Object.entries(fieldErrors)) {
    if (key.startsWith(prefix)) {
      result[key.slice(prefix.length)] = messages;
    }
  }

  return result;
}
