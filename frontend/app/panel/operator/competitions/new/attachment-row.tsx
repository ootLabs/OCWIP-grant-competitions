"use client";

import { attachmentRequirementLabels, fileFormatLabels } from "@/app/competitions/labels";
import type { AllowedFileFormat, AttachmentRequirement } from "@/lib/competitions";
import type { AttachmentDraft } from "@/lib/competition-wizard/types";

import { FieldError } from "./field-error";

const ALL_REQUIREMENTS: AttachmentRequirement[] = [
  "Required",
  "Optional",
  "RequiredOutsideKrs",
];

const ALL_FILE_FORMATS: AllowedFileFormat[] = [
  "Pdf",
  "Doc",
  "Docx",
  "Xls",
  "Xlsx",
  "Jpg",
  "Odt",
  "Ods",
];

/** One required attachment, editable and removable (krok 1.5). */
export function AttachmentRow({
  attachment,
  fieldErrors,
  onChange,
  onRemove,
  groupName,
}: {
  attachment: AttachmentDraft;
  fieldErrors: Record<string, string[]>;
  onChange: (attachment: AttachmentDraft) => void;
  onRemove: () => void;
  /** Unique per row: two rows sharing a name would link their radios. */
  groupName: string;
}) {
  const toggleFormat = (format: AllowedFileFormat, enabled: boolean) => {
    onChange({
      ...attachment,
      allowedFormats: enabled
        ? [...attachment.allowedFormats, format]
        : attachment.allowedFormats.filter((f) => f !== format),
    });
  };

  return (
    <div className="flex flex-col gap-3 rounded-sm border border-border-muted px-3 py-3">
      <label className="flex flex-col gap-1 text-sm">
        Tytuł załącznika
        <input
          className="rounded-sm border border-border px-2 py-1"
          value={attachment.title}
          onChange={(event) =>
            onChange({ ...attachment, title: event.target.value })
          }
        />
        <FieldError messages={fieldErrors.title} />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Opis, czyli co dokładnie ma być dołączone
        <textarea
          className="rounded-sm border border-border px-2 py-1"
          value={attachment.description}
          onChange={(event) =>
            onChange({ ...attachment, description: event.target.value })
          }
        />
        <FieldError messages={fieldErrors.description} />
      </label>

      <fieldset className="flex flex-col gap-1 text-sm">
        <legend>Wymagalność</legend>
        {ALL_REQUIREMENTS.map((requirement) => (
          <label key={requirement} className="flex items-center gap-2">
            <input
              type="radio"
              name={groupName}
              checked={attachment.requirement === requirement}
              onChange={() => onChange({ ...attachment, requirement })}
            />
            {attachmentRequirementLabels[requirement]}
          </label>
        ))}
        <FieldError messages={fieldErrors.requirement} />
      </fieldset>

      <fieldset className="flex flex-wrap gap-3 text-sm">
        <legend>Dopuszczalne formaty pliku</legend>
        {ALL_FILE_FORMATS.map((format) => (
          <label key={format} className="flex items-center gap-1">
            <input
              type="checkbox"
              checked={attachment.allowedFormats.includes(format)}
              onChange={(event) => toggleFormat(format, event.target.checked)}
            />
            {fileFormatLabels[format]}
          </label>
        ))}
        <FieldError messages={fieldErrors.allowedFormats} />
      </fieldset>

      <button type="button" className="self-start underline" onClick={onRemove}>
        Usuń ten załącznik
      </button>
    </div>
  );
}
