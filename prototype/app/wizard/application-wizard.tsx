"use client";

import { useEffect, useRef, useState } from "react";
import { AppBar } from "@/components/AppBar";
import { ScreenFrame } from "@/components/ScreenFrame";
import { Stepper } from "@/components/Stepper";
import { WIZARD_STEPS, WizardStepBody } from "./wizard-steps";

const INITIAL_DESCRIPTION =
  "Chcemy odnowić podwórko między blokami przy ul. Sosnowej. Postawimy dwie ławki, posadzimy żywopłot od strony parkingu i zorganizujemy dwa sobotnie spotkania sąsiedzkie, na których zrobimy to wspólnie z mieszkańcami.";

/** Long enough to be noticed, short enough not to feel like the app is stuck. */
const SAVE_DELAY_MS = 900;

export function ApplicationWizard() {
  const [step, setStep] = useState(2);
  const [description, setDescription] = useState(INITIAL_DESCRIPTION);
  const [saved, setSaved] = useState(true);
  const saveTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    return () => {
      if (saveTimer.current) {
        clearTimeout(saveTimer.current);
      }
    };
  }, []);

  // Autosave is the promise this direction makes, so the indicator is the one
  // piece of behaviour the visualisation actually needs to fake.
  function handleDescriptionChange(value: string) {
    setDescription(value);
    setSaved(false);
    if (saveTimer.current) {
      clearTimeout(saveTimer.current);
    }
    saveTimer.current = setTimeout(() => setSaved(true), SAVE_DELAY_MS);
  }

  const current = WIZARD_STEPS[step];
  const isLast = step === WIZARD_STEPS.length - 1;

  return (
    <ScreenFrame className="max-w-5xl">
      <AppBar
        context="Mikrodotacje na inicjatywy sąsiedzkie 2026"
        account="Grupa nieformalna Łąka"
      />

      <div className="flex flex-col gap-8 px-5 py-7 sm:px-7 lg:flex-row">
        <div className="flex min-w-0 grow flex-col gap-5">
          <div className="flex flex-col gap-2.5">
            <div className="flex flex-wrap items-baseline gap-3">
              <span className="text-[13px] font-semibold">
                Krok {step + 1} z {WIZARD_STEPS.length}
              </span>
              <span className="grow" />
              <span
                className={`text-xs ${saved ? "text-text-link" : "text-brand-accent-text"}`}
                aria-live="polite"
              >
                {saved ? "Zapisano wszystkie zmiany" : "Zapisywanie w tle"}
              </span>
            </div>
            <Stepper
              labels={WIZARD_STEPS.map((wizardStep) => wizardStep.label)}
              current={step}
              onSelect={setStep}
            />
          </div>

          <div className="flex flex-col gap-2.5">
            <h1 className="text-3xl leading-snug">{current.question}</h1>
            <p className="text-base leading-relaxed text-text-link">{current.helper}</p>
          </div>

          <WizardStepBody
            step={step}
            description={description}
            onDescriptionChange={handleDescriptionChange}
          />

          <div className="flex flex-wrap items-center gap-3 pt-1">
            <button
              type="button"
              onClick={() => setStep((value) => Math.min(value + 1, WIZARD_STEPS.length - 1))}
              className="inline-flex min-h-[48px] cursor-pointer items-center justify-center rounded-[var(--radius-sm)] bg-brand-accent px-6 text-[15px] font-semibold text-bg hover:bg-brand-accent-hover"
            >
              {isLast ? "Przejdź do podsumowania" : "Zapisz i przejdź dalej"}
            </button>
            <button
              type="button"
              onClick={() => setStep((value) => Math.max(value - 1, 0))}
              className="inline-flex min-h-[48px] cursor-pointer items-center justify-center rounded-[var(--radius-sm)] border border-border px-5 text-[15px] font-semibold text-text-link hover:border-brand-accent"
            >
              Wróć
            </button>
            <span className="grow" />
            <span className="text-[13px] text-text-link">
              Możecie wyjść i wrócić. Nic nie przepadnie.
            </span>
          </div>
        </div>

        <aside className="flex shrink-0 flex-col gap-3 lg:w-[264px]">
          <div className="flex flex-col gap-1.5 rounded-[var(--radius-sm)] border-2 border-brand-accent p-4">
            <span className="text-xs font-semibold uppercase tracking-[0.08em] text-brand-accent-text">
              Do zamknięcia naboru
            </span>
            <span className="text-xl font-semibold">2 dni i 4 godziny</span>
            <span className="text-[13px] leading-relaxed text-text-link">
              30 września, godz. 12:00. O 12:01 system nie przyjmie już nic.
            </span>
          </div>

          <div className="flex flex-col gap-1.5 rounded-[var(--radius-sm)] bg-surface-muted p-4">
            <span className="text-[13px] font-semibold">Wersja robocza</span>
            <span className="text-[13px] leading-relaxed text-text-link">
              Zapisujemy każdą zmianę w tle. Wniosek ma sześć kroków i nikt nie wypełnia
              go za jednym posiedzeniem.
            </span>
          </div>

          <div className="flex flex-col gap-1.5 rounded-[var(--radius-sm)] bg-surface-muted p-4">
            <span className="text-[13px] font-semibold">Limit dotacji</span>
            <span className="text-lg font-semibold">9 000 zł</span>
            <span className="text-[13px] leading-relaxed text-text-link">
              Na jedno zadanie w tym konkursie.
            </span>
          </div>
        </aside>
      </div>
    </ScreenFrame>
  );
}
