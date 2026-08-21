// WCAG 2.1 relative luminance and contrast ratio (https://www.w3.org/TR/WCAG21/#dfn-relative-luminance).
// Used to verify design token pairs meet AA (4.5:1 text, 3:1 large text/UI) with a tool
// instead of eyeballing hex codes, and to render live PASS/FAIL badges on /design-tokens.

function srgbChannelToLinear(channel: number): number {
  const c = channel / 255;
  return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
}

export function relativeLuminance(hex: string): number {
  const normalized = hex.replace("#", "");
  const r = parseInt(normalized.substring(0, 2), 16);
  const g = parseInt(normalized.substring(2, 4), 16);
  const b = parseInt(normalized.substring(4, 6), 16);
  const [rLin, gLin, bLin] = [r, g, b].map(srgbChannelToLinear);
  return 0.2126 * rLin + 0.7152 * gLin + 0.0722 * bLin;
}

export function contrastRatio(hexA: string, hexB: string): number {
  const lumA = relativeLuminance(hexA);
  const lumB = relativeLuminance(hexB);
  const lighter = Math.max(lumA, lumB);
  const darker = Math.min(lumA, lumB);
  return (lighter + 0.05) / (darker + 0.05);
}

export const WCAG_AA_TEXT = 4.5;
export const WCAG_AA_LARGE_TEXT = 3;

export function meetsAA(hexA: string, hexB: string, largeText = false): boolean {
  const threshold = largeText ? WCAG_AA_LARGE_TEXT : WCAG_AA_TEXT;
  return contrastRatio(hexA, hexB) >= threshold;
}
