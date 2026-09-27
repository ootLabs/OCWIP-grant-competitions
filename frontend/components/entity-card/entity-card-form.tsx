"use client";

import { useId, useState } from "react";

import { accountSubmitClassName } from "@/components/account-field";
import { ApiError } from "@/lib/api-client";
import {
  entityTypeChoices,
  entityTypeHints,
  entityTypeLabels,
  hasOrganisationCard,
  legalFormLabels,
  registerLabels,
  saveMyEntityCard,
  type EntityCard,
  type EntityCardResponse,
  type EntityRegister,
  type LegalForm,
} from "@/lib/entity-card";
import type { EntityType } from "@/lib/operator-applications";

import { CardField, FieldErrorList, Select } from "./card-field";
import { RepresentativesTable } from "./representatives-table";

type FieldErrors = Record<string, string[]>;

/**
 * The Podmiot card as a form (T-93, pola.md step 2.2): empty at the first
 * application, filled in when corrected. Every rule and checksum is the
 * backend's (EntityCardValidator); this form sends what was typed and puts
 * the backend's sentences next to the fields they belong to.
 *
 * An informal group without a patron has no card (pola.md, type 3), so
 * choosing it hides everything but the name.
 */
export function EntityCardForm({
  initial,
  exists,
  submitLabel,
  onSaved,
  onCancel,
}: {
  initial: EntityCard;
  /** PUT a correction rather than POST the first card. */
  exists: boolean;
  submitLabel: string;
  onSaved: (saved: EntityCardResponse) => void;
  onCancel?: () => void;
}) {
  const [card, setCard] = useState<EntityCard>(initial);
  const [otherAddress, setOtherAddress] = useState(Boolean(initial.correspondenceAddress));
  const [errors, setErrors] = useState<FieldErrors>({});
  const [failure, setFailure] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const selectId = useId();

  const set = (patch: Partial<EntityCard>) => setCard((current) => ({ ...current, ...patch }));
  const text = (value: string | null | undefined) => value ?? "";
  const organisation = hasOrganisationCard(card.type);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setSaving(true);
    setErrors({});
    setFailure(null);

    try {
      const body: EntityCard = {
        ...card,
        correspondenceAddress: otherAddress ? card.correspondenceAddress : null,
      };
      onSaved(await saveMyEntityCard(body, exists));
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setErrors(error.fieldErrors);
        setFailure("Popraw zaznaczone pola.");
      } else if (error instanceof ApiError && error.detail) {
        setFailure(error.detail);
      } else {
        setFailure("Nie udało się zapisać danych. Spróbuj ponownie.");
      }
    } finally {
      setSaving(false);
    }
  }

  return (
    <form className="flex flex-col gap-5" noValidate onSubmit={submit}>
      {failure !== null ? (
        <p role="alert" className="text-sm text-brand-accent-text">
          {failure}
        </p>
      ) : null}

      <fieldset className="flex flex-col gap-2">
        <legend className="text-base">Kto składa wniosek</legend>
        {entityTypeChoices.map((type: EntityType) => (
          <label key={type} className="flex items-start gap-2 text-sm">
            <input
              type="radio"
              name="type"
              className="mt-1"
              checked={card.type === type}
              onChange={() => set({ type })}
            />
            <span>
              {entityTypeLabels[type]}
              <span className="block">{entityTypeHints[type]}</span>
            </span>
          </label>
        ))}
        <FieldErrorList errors={errors["type"]} />
      </fieldset>

      <CardField
        label={organisation ? "Pełna nazwa organizacji" : "Nazwa grupy"}
        name="name"
        required
        autoComplete="organization"
        value={card.name}
        onChange={(name) => set({ name })}
        errors={errors["name"]}
      />

      {organisation ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              id={`${selectId}-legal-form`}
              label="Forma prawna"
              value={card.legalForm ?? ""}
              options={legalFormLabels}
              onChange={(value) => set({ legalForm: (value || null) as LegalForm | null })}
              errors={errors["legalForm"]}
            />
            {card.legalForm === "Other" ? (
              <CardField
                label="Nazwa formy prawnej"
                name="legalFormOther"
                required
                value={text(card.legalFormOther)}
                onChange={(legalFormOther) => set({ legalFormOther })}
                errors={errors["legalFormOther"]}
              />
            ) : null}
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <Select
              id={`${selectId}-register`}
              label="Rejestr"
              value={card.register ?? ""}
              options={registerLabels}
              onChange={(value) => set({ register: (value || null) as EntityRegister | null })}
              errors={errors["register"]}
            />
            <CardField
              label={card.register === "Krs" ? "Numer KRS" : "Numer w rejestrze"}
              name="registerNumber"
              required
              inputMode={card.register === "Krs" ? "numeric" : "text"}
              hint={card.register === "Krs" ? "10 cyfr, razem z zerami na początku." : undefined}
              value={text(card.registerNumber)}
              onChange={(registerNumber) => set({ registerNumber })}
              errors={errors["registerNumber"]}
            />
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <CardField
              label="NIP"
              name="nip"
              required
              inputMode="numeric"
              value={text(card.nip)}
              onChange={(nip) => set({ nip })}
              errors={errors["nip"]}
            />
            <CardField
              label="REGON"
              name="regon"
              inputMode="numeric"
              value={text(card.regon)}
              onChange={(regon) => set({ regon })}
              errors={errors["regon"]}
            />
          </div>

          <CardField
            label="Adres siedziby"
            name="address"
            required
            autoComplete="street-address"
            value={text(card.address)}
            onChange={(address) => set({ address })}
            errors={errors["address"]}
          />
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={otherAddress}
              onChange={(event) => setOtherAddress(event.target.checked)}
            />
            Adres do korespondencji jest inny niż adres siedziby
          </label>
          {otherAddress ? (
            <CardField
              label="Adres do korespondencji"
              name="correspondenceAddress"
              required
              value={text(card.correspondenceAddress)}
              onChange={(correspondenceAddress) => set({ correspondenceAddress })}
              errors={errors["correspondenceAddress"]}
            />
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <CardField
              label="Telefon"
              name="phone"
              required
              type="tel"
              autoComplete="tel"
              value={text(card.phone)}
              onChange={(phone) => set({ phone })}
              errors={errors["phone"]}
            />
            <CardField
              label="E-mail"
              name="email"
              required
              type="email"
              autoComplete="email"
              value={text(card.email)}
              onChange={(email) => set({ email })}
              errors={errors["email"]}
            />
          </div>

          <CardField
            label="Numer rachunku bankowego"
            name="bankAccount"
            required
            inputMode="numeric"
            hint="26 cyfr. Na ten rachunek trafi dotacja."
            value={text(card.bankAccount)}
            onChange={(bankAccount) => set({ bankAccount })}
            errors={errors["bankAccount"]}
          />

          <RepresentativesTable
            rows={card.representatives ?? []}
            onChange={(representatives) => set({ representatives })}
            errors={errors}
          />
        </>
      ) : null}

      <div className="flex flex-col gap-3 sm:flex-row">
        <button type="submit" className={accountSubmitClassName} disabled={saving}>
          {saving ? "Zapisywanie…" : submitLabel}
        </button>
        {onCancel ? (
          <button type="button" className="text-sm underline" onClick={onCancel}>
            Anuluj
          </button>
        ) : null}
      </div>
    </form>
  );
}
