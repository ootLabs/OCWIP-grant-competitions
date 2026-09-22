"use client";

import { slugifyKey } from "@/lib/forms/document-keys";
import type { FormOption } from "@/lib/forms/document-types";

/**
 * The choice list of singleChoice/multipleChoice (T-26). An operator types a
 * label; the value stored on the wire is derived from it the same way a
 * field's own key is, so nothing technical is ever typed here either.
 */
export function OptionsEditor({
  options,
  onChange,
}: {
  options: readonly FormOption[];
  onChange: (options: FormOption[]) => void;
}) {
  const taken = new Set(options.map((option) => option.value));

  return (
    <div className="flex flex-col gap-2">
      <span className="text-sm">Opcje do wyboru</span>
      {options.length === 0 ? (
        <p className="text-sm">Ta lista nie ma jeszcze żadnej opcji.</p>
      ) : null}
      <ul className="flex flex-col gap-2">
        {options.map((option, index) => (
          <li key={option.value} className="flex items-center gap-2">
            <input
              className="flex-1 rounded-sm border border-border px-2 py-1 text-sm"
              value={option.label}
              aria-label={`Opcja ${index + 1}`}
              onChange={(event) => {
                const next = [...options];
                next[index] = { ...option, label: event.target.value };
                onChange(next);
              }}
            />
            <button
              type="button"
              className="text-sm underline"
              onClick={() => onChange(options.filter((_, i) => i !== index))}
            >
              Usuń
            </button>
          </li>
        ))}
      </ul>
      <button
        type="button"
        className="self-start text-sm underline"
        onClick={() => {
          const label = `Opcja ${options.length + 1}`;
          let value = slugifyKey(label);
          let suffix = 2;
          while (taken.has(value)) {
            value = `${slugifyKey(label)}_${suffix}`;
            suffix += 1;
          }
          onChange([...options, { value, label }]);
        }}
      >
        Dodaj opcję
      </button>
    </div>
  );
}
