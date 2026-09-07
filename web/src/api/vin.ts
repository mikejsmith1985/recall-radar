// What counts as a VIN, checked in the page so a mistyped one never reaches a load.

/** Every VIN issued since 1981 is exactly this long. */
export const VinLength = 17;

/** Letters the standard leaves out, because they are read as 1 and 0. */
export const ForbiddenVinLetters = "IOQ";

/**
 * Whether text is a plausible VIN.
 *
 * Mirrors the server's rule rather than replacing it: the server still checks, because the page is
 * not the only caller. Checking here saves a round trip and says so beside the field being typed.
 */
export function isWellFormedVin(vin: string | null | undefined): boolean {
  if (!vin) {
    return false;
  }
  const trimmed = vin.trim().toUpperCase();
  if (trimmed.length !== VinLength) {
    return false;
  }
  return [...trimmed].every((character) => /[A-Z0-9]/.test(character) && !ForbiddenVinLetters.includes(character));
}
