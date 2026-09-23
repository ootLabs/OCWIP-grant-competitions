import { describe, expect, it } from "vitest";

import { amountInWords, integerInWords } from "./amount-in-words";

describe("integerInWords", () => {
  it("writes zero", () => {
    expect(integerInWords(0)).toBe("zero");
  });

  it("writes the ones", () => {
    expect(integerInWords(1)).toBe("jeden");
    expect(integerInWords(9)).toBe("dziewięć");
  });

  it("writes the teens, which do not compose from tens and ones", () => {
    expect(integerInWords(10)).toBe("dziesięć");
    expect(integerInWords(15)).toBe("piętnaście");
    expect(integerInWords(19)).toBe("dziewiętnaście");
  });

  it("writes tens plus ones", () => {
    expect(integerInWords(20)).toBe("dwadzieścia");
    expect(integerInWords(21)).toBe("dwadzieścia jeden");
    expect(integerInWords(99)).toBe("dziewięćdziesiąt dziewięć");
  });

  it("writes hundreds, including a bare hundred", () => {
    expect(integerInWords(100)).toBe("sto");
    expect(integerInWords(105)).toBe("sto pięć");
    expect(integerInWords(999)).toBe(
      "dziewięćset dziewięćdziesiąt dziewięć",
    );
  });

  it("writes a bare thousand as \"tysiąc\", never \"jeden tysiąc\"", () => {
    expect(integerInWords(1000)).toBe("tysiąc");
    expect(integerInWords(1001)).toBe("tysiąc jeden");
  });

  it("writes thousands with their own count", () => {
    expect(integerInWords(2000)).toBe("dwa tysiące");
    expect(integerInWords(5000)).toBe("pięć tysięcy");
    expect(integerInWords(12000)).toBe("dwanaście tysięcy");
    expect(integerInWords(22000)).toBe("dwadzieścia dwa tysiące");
    expect(integerInWords(120000)).toBe("sto dwadzieścia tysięcy");
  });

  it("writes a thousands group together with the remainder below it", () => {
    expect(integerInWords(120450)).toBe(
      "sto dwadzieścia tysięcy czterysta pięćdziesiąt",
    );
  });

  it("writes millions", () => {
    expect(integerInWords(1_000_000)).toBe("milion");
    expect(integerInWords(2_000_000)).toBe("dwa miliony");
    expect(integerInWords(5_000_000)).toBe("pięć milionów");
  });

  it("writes millions, thousands and the remainder together", () => {
    expect(integerInWords(3_040_007)).toBe(
      "trzy miliony czterdzieści tysięcy siedem",
    );
  });

  it("rejects a negative number", () => {
    expect(() => integerInWords(-1)).toThrow(RangeError);
  });

  it("rejects a non integer", () => {
    expect(() => integerInWords(1.5)).toThrow(RangeError);
  });
});

describe("amountInWords", () => {
  it("writes a round amount without mentioning grosze", () => {
    expect(amountInWords(100)).toBe("sto złotych");
  });

  it("picks the singular for exactly one złoty", () => {
    expect(amountInWords(1)).toBe("jeden złoty");
  });

  it("picks the few form for 2 to 4, teens excepted", () => {
    expect(amountInWords(2)).toBe("dwa złote");
    expect(amountInWords(22)).toBe("dwadzieścia dwa złote");
    expect(amountInWords(12)).toBe("dwanaście złotych");
  });

  it("a compound number ending in one still takes the plural, not the singular", () => {
    // The well known exception: only the number 1 itself is singular. 21,
    // 101 and 1001 all take "złotych", matching how a price is actually read
    // aloud in Polish.
    expect(amountInWords(21)).toBe("dwadzieścia jeden złotych");
    expect(amountInWords(1001)).toBe("tysiąc jeden złotych");
  });

  it("adds grosze when the amount carries them", () => {
    expect(amountInWords(120.5)).toBe(
      "sto dwadzieścia złotych i pięćdziesiąt groszy",
    );
    expect(amountInWords(1)).not.toContain("grosz");
  });

  it("picks the grosz plural form the same way as złoty", () => {
    expect(amountInWords(0.01)).toBe("zero złotych i jeden grosz");
    expect(amountInWords(0.02)).toBe("zero złotych i dwa grosze");
    expect(amountInWords(0.05)).toBe("zero złotych i pięć groszy");
  });

  it("rounds to the grosz instead of truncating float noise", () => {
    // 19.999999999999996 is what 0.1 + 19.9 gives back in floating point;
    // the amount actually meant is twenty złoty even.
    expect(amountInWords(0.1 + 19.9)).toBe("dwadzieścia złotych");
  });

  it("accepts the string shape OpenAPI sends for a decimal", () => {
    expect(amountInWords("2500.00")).toBe("dwa tysiące pięćset złotych");
  });

  it("rejects a negative amount", () => {
    expect(() => amountInWords(-5)).toThrow(RangeError);
  });
});
