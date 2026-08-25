type StepperProps = {
  labels: string[];
  /** Zero based index of the step being filled in. */
  current: number;
  /** Omitted on read-only screens, where the bar only reports progress. */
  onSelect?: (index: number) => void;
  showLabels?: boolean;
};

/**
 * Progress in direction C is not decoration: the whole promise of the direction
 * is that someone filling a six page form always knows how much is left without
 * clicking anything.
 */
export function Stepper({ labels, current, onSelect, showLabels = true }: StepperProps) {
  return (
    <div className="flex gap-1.5">
      {labels.map((label, index) => {
        const reached = index <= current;
        const bar = `block h-2 rounded-[var(--radius-pill)] ${
          reached ? "bg-brand-accent" : "bg-border-muted"
        }`;

        if (!onSelect) {
          return (
            <div key={index} className="flex grow basis-0 flex-col gap-1.5">
              <span className={bar} />
              {showLabels && (
                <span
                  className={`text-[11px] ${index === current ? "text-text" : "text-text-link"}`}
                >
                  {label}
                </span>
              )}
            </div>
          );
        }

        return (
          <button
            key={index}
            type="button"
            onClick={() => onSelect(index)}
            aria-current={index === current ? "step" : undefined}
            className="flex grow basis-0 cursor-pointer flex-col gap-1.5 text-left"
          >
            <span className={bar} />
            {showLabels && (
              <span
                className={`text-[11px] ${index === current ? "text-text" : "text-text-link"}`}
              >
                {label}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
