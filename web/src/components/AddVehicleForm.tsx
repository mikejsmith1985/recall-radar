// Registers a vehicle and starts its load, so adding a car never means opening a terminal.
import { useEffect, useState } from "react";
import type { ApiClient, Load, RegisterVehicleRequest } from "../api/client";
import { isWellFormedVin, VinLength } from "../api/vin";

interface AddVehicleFormProps {
  client: ApiClient;
  onLoadStarted: (load: Load) => void;
  /**
   * Whether the form is showing. Held by the caller so the button that opens it can sit above the
   * vehicle list, where nothing that loads later can move it out from under a pointer.
   */
  isOpen: boolean;
  onOpenChange: (isOpen: boolean) => void;
}

/** NHTSA files records a model year ahead of the calendar, and no further. */
export const EarliestModelYear = 1949;
export function latestModelYear(now: Date = new Date()): number {
  return now.getFullYear() + 1;
}

/** The first thing wrong with the form, or null when it is ready to send. */
export function findProblem(request: RegisterVehicleRequest, now: Date = new Date()): string | null {
  if (!request.make.trim()) {
    return 'A make is required, for example "Ford".';
  }
  if (!request.nhtsaModel.trim()) {
    return 'An NHTSA model name is required, for example "EXPLORER".';
  }
  if (!request.displayName.trim()) {
    return 'A name is required, for example "2013 Explorer Sport".';
  }
  if (!Number.isInteger(request.modelYear) || request.modelYear < EarliestModelYear || request.modelYear > latestModelYear(now)) {
    return `The model year must be between ${EarliestModelYear} and ${latestModelYear(now)}.`;
  }
  // Optional, but a half-typed one is worse than none: it would decode to a different vehicle.
  if (request.vin && !isWellFormedVin(request.vin)) {
    return `A VIN is ${VinLength} letters and digits, never I, O or Q. Leave it blank if you do not have it to hand.`;
  }
  return null;
}

const EmptyForm: RegisterVehicleRequest = {
  make: "Ford",
  nhtsaModel: "",
  recallModel: "",
  modelYear: new Date().getFullYear(),
  displayName: "",
  vin: "",
};

export function AddVehicleForm({ client, onLoadStarted, isOpen, onOpenChange }: AddVehicleFormProps) {
  const [form, setForm] = useState<RegisterVehicleRequest>(EmptyForm);
  const [problem, setProblem] = useState<string | null>(null);
  const [isSending, setIsSending] = useState(false);
  const [knownModels, setKnownModels] = useState<string[]>([]);

  // NHTSA's vocabulary is not the one on the back of the car: a Mach-E is filed as
  // "MUSTANG MACH-E BEV BEV". Offering the real names beats making anyone guess them.
  const { make, modelYear } = form;
  useEffect(() => {
    if (!isOpen || !make.trim() || !Number.isInteger(modelYear) || modelYear < EarliestModelYear) {
      return;
    }

    let isCurrent = true;
    client
      .listNhtsaModels(make.trim(), modelYear)
      .then((names) => isCurrent && setKnownModels(names))
      // The list is a convenience. Losing it must not stop anyone typing the name themselves.
      .catch(() => isCurrent && setKnownModels([]));
    return () => {
      isCurrent = false;
    };
  }, [client, isOpen, make, modelYear]);

  function update(field: keyof RegisterVehicleRequest, value: string) {
    setForm((current) => ({
      ...current,
      [field]: field === "modelYear" ? Number.parseInt(value, 10) || 0 : value,
    }));
  }

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const found = findProblem(form);
    if (found) {
      setProblem(found);
      return;
    }

    setProblem(null);
    setIsSending(true);
    try {
      // The load takes minutes, so the server answers with a job rather than a vehicle. The caller
      // decides what to show while it runs.
      onLoadStarted(await client.registerVehicle({
        ...form,
        recallModel: form.recallModel?.trim() || null,
        vin: form.vin?.trim().toUpperCase() || null,
      }));
      setForm(EmptyForm);
      onOpenChange(false);
    } catch (error: unknown) {
      // NHTSA's own rejection names the model strings it does know, which is the useful part.
      setProblem(error instanceof Error ? error.message : "The vehicle could not be registered.");
    } finally {
      setIsSending(false);
    }
  }

  if (!isOpen) {
    return null;
  }

  return (
    <form className="add-vehicle-form" onSubmit={submit} data-testid="add-vehicle-form">
      <label>
        Make
        <input aria-label="Make" value={form.make} onChange={(event) => update("make", event.target.value)} />
      </label>
      <label>
        NHTSA model
        <input
          aria-label="NHTSA model"
          placeholder="EXPLORER"
          list="nhtsa-model-names"
          value={form.nhtsaModel}
          onChange={(event) => update("nhtsaModel", event.target.value)}
        />
        <datalist id="nhtsa-model-names" data-testid="nhtsa-model-names">
          {knownModels.map((name) => (
            <option key={name} value={name} />
          ))}
        </datalist>
        {knownModels.length > 0 && (
          <small data-testid="model-count">
            {knownModels.length} names on file for {form.make.trim()} {form.modelYear}
          </small>
        )}
      </label>
      <label>
        Model year
        <input
          aria-label="Model year"
          type="number"
          value={form.modelYear}
          onChange={(event) => update("modelYear", event.target.value)}
        />
      </label>
      <label>
        Your name for it
        <input
          aria-label="Your name for it"
          placeholder="2013 Explorer Sport"
          value={form.displayName}
          onChange={(event) => update("displayName", event.target.value)}
        />
      </label>
      <label>
        VIN <small>(optional, narrows to your exact trim)</small>
        <input
          aria-label="VIN"
          placeholder="1FTFW1RJ0PFB00000"
          maxLength={VinLength}
          value={form.vin ?? ""}
          onChange={(event) => update("vin", event.target.value.toUpperCase())}
        />
      </label>
      <label>
        Recalls model <small>(only if it differs)</small>
        <input
          aria-label="Recalls model"
          placeholder="F-150"
          value={form.recallModel ?? ""}
          onChange={(event) => update("recallModel", event.target.value)}
        />
      </label>
      {problem && (
        <p className="error-state" data-testid="add-vehicle-problem">
          {problem}
        </p>
      )}
      <div className="form-actions">
        <button type="submit" disabled={isSending} data-testid="add-vehicle-submit">
          {isSending ? "Starting…" : "Add and load"}
        </button>
        <button type="button" className="link-button" data-testid="add-vehicle-cancel" onClick={() => onOpenChange(false)}>
          Cancel
        </button>
      </div>
    </form>
  );
}
