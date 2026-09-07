// Chooses whether a search stays with this exact truck or opens to every version of the model.
import type { TrimScope } from "../api/client";

interface TrimSwitchProps {
  trims: TrimScope;
  /** What the vehicle's VIN decoded to, or null when no VIN has said which version it is. */
  vehicleTrim: string | null;
  /**
   * How many of this vehicle's records belong to a different trim or engine, or null before a
   * search has said. Null rather than zero, because zero is a claim and this would be guessing.
   */
  otherTrims: number | null;
  onChange: (trims: TrimScope) => void;
}

/** Shown when no VIN has been given, because nothing can be narrowed without one. */
export const NoVinExplanation =
  "Add this vehicle's VIN to search only its own trim and engine. NHTSA files every version of a model under one name.";

/** Describes what the narrow option would leave out, so choosing it is an informed choice. */
export function describeOtherTrims(otherTrims: number): string {
  if (otherTrims === 0) {
    return "Every record on file is from this trim and engine.";
  }
  return otherTrims === 1
    ? "1 record on file is from another version of this model."
    : `${otherTrims} records on file are from other versions of this model.`;
}

export function TrimSwitch({ trims, vehicleTrim, otherTrims, onChange }: TrimSwitchProps) {
  // Without a decoded VIN there is nothing to narrow to, so the choice would be a lie.
  if (vehicleTrim === null) {
    return (
      <p className="caption" data-testid="trim-needs-vin">
        {NoVinExplanation}
      </p>
    );
  }

  return (
    <div className="trim-switch" data-testid="trim-switch">
      <div role="radiogroup" aria-label="Which versions to search">
        <button
          type="button"
          role="radio"
          aria-checked={trims === "thisTrim"}
          data-testid="trims-thisTrim"
          onClick={() => onChange("thisTrim")}
        >
          {vehicleTrim}
        </button>
        <button
          type="button"
          role="radio"
          aria-checked={trims === "allTrims"}
          data-testid="trims-allTrims"
          onClick={() => onChange("allTrims")}
        >
          Every version
        </button>
      </div>
      {otherTrims !== null && (
        <span className="caption" data-testid="trim-explanation">
          {describeOtherTrims(otherTrims)}
        </span>
      )}
    </div>
  );
}
