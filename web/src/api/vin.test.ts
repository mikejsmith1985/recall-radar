// Checks the page's own VIN rule, which saves a round trip and matches what the server will say.
import { ForbiddenVinLetters, isWellFormedVin, VinLength } from "./vin";

describe("isWellFormedVin", () => {
  it("accepts a real VIN", () => {
    expect(isWellFormedVin("1FTFW1RJ0PFB00000")).toBe(true);
  });

  it("accepts one typed in lower case with space around it", () => {
    expect(isWellFormedVin("  1ftfw1rj0pfb00000 ")).toBe(true);
  });

  it("refuses the wrong length", () => {
    // A half-typed VIN is worse than none: it would decode to a different vehicle.
    expect(isWellFormedVin("1FTFW1RJ6PFC9872")).toBe(false);
    expect(isWellFormedVin("1FTFW1RJ0PFB000000")).toBe(false);
  });

  it.each([...ForbiddenVinLetters])("refuses %s, which the standard leaves out", (letter) => {
    expect(isWellFormedVin(`1FTFW1RJ6PFC${letter}8720`.slice(0, VinLength))).toBe(false);
  });

  it("refuses punctuation", () => {
    expect(isWellFormedVin("1FTFW1RJ-PFC9872")).toBe(false);
  });

  it("treats nothing as not a VIN, which is not an error", () => {
    expect(isWellFormedVin(null)).toBe(false);
    expect(isWellFormedVin(undefined)).toBe(false);
    expect(isWellFormedVin("   ")).toBe(false);
  });
});
