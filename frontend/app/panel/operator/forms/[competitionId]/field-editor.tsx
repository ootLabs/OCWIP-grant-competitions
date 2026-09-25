"use client";

import {
  ALLOWED_FILE_FORMATS,
  CHOICE_TYPES,
  NUMERIC_TYPES,
  TABLE_TYPES,
  TEXTUAL_TYPES,
  type AllowedFileFormat,
  type FormDocument,
  type FormField,
} from "@/lib/forms/document-types";
import { ALLOWED_FILE_FORMAT_LABELS } from "@/lib/forms/labels";
import { CalculationEditor } from "./calculation-editor";
import { LimitsEditor } from "./limits-editor";
import { OptionsEditor } from "./options-editor";
import { RolePicker } from "./role-picker";
import { TableEditor } from "./table-editor";
import { VisibleWhenEditor } from "./visible-when-editor";

/**
 * Every setting a field can carry, common controls first and then whatever
 * its kind adds (card T-26: "Zero żargonu w etykietach, zero JSON-a, zero
 * regexów wystawionych użytkownikowi"). A table column reuses this whole
 * component, minus the visibility condition: a column's own condition is not
 * part of this card's scope, only the table it sits in can be conditional.
 */
export function FieldEditor({
  document,
  sectionKey,
  fieldKey,
  columnKey,
  field,
  onChange,
}: {
  document: FormDocument;
  sectionKey: string;
  fieldKey: string;
  columnKey?: string;
  field: FormField;
  onChange: (field: FormField) => void;
}) {
  const isColumn = columnKey !== undefined;

  return (
    <div className="flex flex-col gap-3">
      <label className="flex flex-col gap-1 text-sm">
        Etykieta
        <input
          className="rounded-sm border border-border-control px-2 py-1"
          value={field.label}
          onChange={(event) => onChange({ ...field, label: event.target.value })}
        />
      </label>

      <label className="flex flex-col gap-1 text-sm">
        Podpowiedź dla wnioskodawcy
        <textarea
          className="rounded-sm border border-border-control px-2 py-1"
          value={field.help}
          onChange={(event) => onChange({ ...field, help: event.target.value })}
        />
      </label>

      <div className="flex flex-wrap gap-4">
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={field.required}
            onChange={(event) => onChange({ ...field, required: event.target.checked })}
          />
          Pole wymagane
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={field.printed}
            onChange={(event) => onChange({ ...field, printed: event.target.checked })}
          />
          Widoczne na wydruku oferty
        </label>
      </div>

      {!isColumn ? <RolePicker document={document} field={field} onChange={onChange} /> : null}

      {TEXTUAL_TYPES.has(field.type) ? (
        <TextLimits field={field} onChange={onChange} />
      ) : null}

      {field.type === "number" || field.type === "amount" || field.type === "percent" ? (
        <NumberRange field={field} onChange={onChange} />
      ) : null}

      {CHOICE_TYPES.has(field.type) ? (
        <OptionsEditor
          options={field.options ?? []}
          onChange={(options) => onChange({ ...field, options })}
        />
      ) : null}

      {field.type === "file" ? <FileRules field={field} onChange={onChange} /> : null}

      {field.type === "statement" ? (
        <label className="flex flex-col gap-1 text-sm">
          Treść oświadczenia
          <textarea
            className="rounded-sm border border-border-control px-2 py-1"
            value={field.statementText ?? ""}
            onChange={(event) => onChange({ ...field, statementText: event.target.value })}
          />
        </label>
      ) : null}

      {field.type === "calculated" && field.calculation ? (
        <CalculationEditor
          document={document}
          sectionKey={sectionKey}
          fieldKey={fieldKey}
          columnKey={columnKey}
          value={field.calculation}
          onChange={(calculation) => onChange({ ...field, calculation })}
        />
      ) : null}

      {NUMERIC_TYPES.has(field.type) && !isColumn ? (
        <LimitsEditor
          document={document}
          sectionKey={sectionKey}
          fieldKey={fieldKey}
          limits={field.limits ?? []}
          onChange={(limits) => onChange({ ...field, limits })}
        />
      ) : null}

      {TABLE_TYPES.has(field.type) && field.table ? (
        <TableEditor
          document={document}
          sectionKey={sectionKey}
          fieldKey={field.key}
          table={field.table}
          onChange={(table) => onChange({ ...field, table })}
        />
      ) : null}

      {!isColumn ? (
        <div className="flex flex-col gap-1 border-t border-border-muted pt-3">
          <span className="text-sm">Warunek widoczności</span>
          <VisibleWhenEditor
            document={document}
            sectionKey={sectionKey}
            fieldKey={fieldKey}
            value={field.visibleWhen}
            onChange={(visibleWhen) => onChange({ ...field, visibleWhen })}
          />
        </div>
      ) : null}
    </div>
  );
}

function TextLimits({
  field,
  onChange,
}: {
  field: FormField;
  onChange: (field: FormField) => void;
}) {
  return (
    <div className="flex flex-wrap gap-4">
      <label className="flex items-center gap-2 text-sm">
        Limit znaków
        <input
          type="number"
          min={1}
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={field.maxLength ?? ""}
          onChange={(event) => {
            // maxLength is required on this kind (docs/kontrakt-formularza.md),
            // so a momentarily empty input while retyping keeps the previous
            // value rather than becoming 0, which would be just as invalid but
            // silent about it.
            if (event.target.value === "") {
              return;
            }
            onChange({ ...field, maxLength: Number(event.target.value) });
          }}
        />
      </label>
      {field.type === "longText" ? (
        <label className="flex items-center gap-2 text-sm">
          Minimalna liczba znaków
          <input
            type="number"
            min={0}
            className="w-24 rounded-sm border border-border-control px-2 py-1"
            value={field.minLength ?? ""}
            onChange={(event) =>
              onChange({
                ...field,
                minLength: event.target.value === "" ? undefined : Number(event.target.value),
              })
            }
          />
        </label>
      ) : null}
    </div>
  );
}

function NumberRange({
  field,
  onChange,
}: {
  field: FormField;
  onChange: (field: FormField) => void;
}) {
  return (
    <div className="flex flex-wrap gap-4">
      <label className="flex items-center gap-2 text-sm">
        Wartość minimalna
        <input
          type="number"
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={field.minValue ?? ""}
          onChange={(event) =>
            onChange({
              ...field,
              minValue: event.target.value === "" ? undefined : Number(event.target.value),
            })
          }
        />
      </label>
      <label className="flex items-center gap-2 text-sm">
        Wartość maksymalna
        <input
          type="number"
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={field.maxValue ?? ""}
          onChange={(event) =>
            onChange({
              ...field,
              maxValue: event.target.value === "" ? undefined : Number(event.target.value),
            })
          }
        />
      </label>
    </div>
  );
}

function FileRules({
  field,
  onChange,
}: {
  field: FormField;
  onChange: (field: FormField) => void;
}) {
  const rules = field.file ?? { allowedFormats: [], maxSizeMegabytes: 10 };

  return (
    <div className="flex flex-col gap-2">
      <fieldset className="flex flex-wrap gap-3">
        <legend className="text-sm">Dozwolone formaty pliku</legend>
        {ALLOWED_FILE_FORMATS.map((format) => (
          <label key={format} className="flex items-center gap-1 text-sm">
            <input
              type="checkbox"
              checked={rules.allowedFormats.includes(format)}
              onChange={(event) => {
                const allowedFormats: AllowedFileFormat[] = event.target.checked
                  ? [...rules.allowedFormats, format]
                  : rules.allowedFormats.filter((f) => f !== format);
                onChange({ ...field, file: { ...rules, allowedFormats } });
              }}
            />
            {ALLOWED_FILE_FORMAT_LABELS[format]}
          </label>
        ))}
      </fieldset>
      <label className="flex items-center gap-2 text-sm">
        Maksymalny rozmiar pliku (MB)
        <input
          type="number"
          min={1}
          className="w-24 rounded-sm border border-border-control px-2 py-1"
          value={rules.maxSizeMegabytes}
          onChange={(event) => {
            // Same guard as the character limit above: required, so an
            // emptied input keeps the previous value instead of becoming 0.
            if (event.target.value === "") {
              return;
            }
            onChange({
              ...field,
              file: { ...rules, maxSizeMegabytes: Number(event.target.value) },
            });
          }}
        />
      </label>
    </div>
  );
}
