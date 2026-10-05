/**
 * The five colours a status badge comes in (components/ui/status-badge.tsx).
 *
 * A type of its own in lib, so the label maps next to the data (lib/*,
 * app/competitions/labels.ts) can say which colour a state is without
 * reaching into the components.
 */
export type StatusTone = "positive" | "attention" | "neutral" | "info" | "negative";
