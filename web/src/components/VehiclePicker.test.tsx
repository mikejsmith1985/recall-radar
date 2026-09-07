// Checks the vehicle picker renders every vehicle with its counts and reports a selection.
import { fireEvent, render, screen } from "@testing-library/react";
import { formatCounts, pluralise, VehiclePicker } from "./VehiclePicker";
import type { Vehicle } from "../api/client";

const vehicles: Vehicle[] = [
  { id: 1, displayName: "2013 Explorer Sport", make: "FORD", modelYear: 2013, counts: { complaint: 2231, recall: 12, investigation: 4 } },
  { id: 2, displayName: "2014 F-150 SVT Raptor", make: "FORD", modelYear: 2014, counts: { complaint: 1362, recall: 8, investigation: 2 } },
];

describe("counts", () => {
  it("does not say one complaints", () => {
    // A 2026 car with a single complaint on file is the ordinary first state, not a rare one.
    expect(formatCounts({ ...vehicles[0], counts: { complaint: 1, recall: 0, investigation: 1 } }))
      .toBe("1 complaint · 0 recalls · 1 investigation");
  });

  it("pluralises everything else, including zero", () => {
    expect(pluralise(0, "recall")).toBe("0 recalls");
    expect(pluralise(2, "recall")).toBe("2 recalls");
  });
});

describe("VehiclePicker", () => {
  it("shows every vehicle with its record counts and marks the selected one", () => {
    render(<VehiclePicker vehicles={vehicles} selectedVehicleId={2} onSelect={() => undefined} />);

    const options = screen.getAllByRole("radio");
    expect(options).toHaveLength(2);
    expect(options[0].textContent).toContain("2013 Explorer Sport");
    expect(options[0].textContent).toContain("2231 complaints");
    expect(options[0].getAttribute("aria-checked")).toBe("false");
    expect(options[1].getAttribute("aria-checked")).toBe("true");
  });

  it("reports the clicked vehicle id", () => {
    const selected: number[] = [];
    render(<VehiclePicker vehicles={vehicles} selectedVehicleId={null} onSelect={(vehicleId) => selected.push(vehicleId)} />);

    fireEvent.click(screen.getByTestId("vehicle-option-1"));

    expect(selected).toEqual([1]);
  });

  it("points at the form rather than a command line when there are no vehicles", () => {
    // Adding a vehicle is something the app does now, so the empty state must not send someone
    // to a terminal they may not have.
    render(<VehiclePicker vehicles={[]} selectedVehicleId={null} onSelect={() => undefined} />);

    const empty = screen.getByTestId("vehicle-picker-empty").textContent ?? "";
    expect(empty).toContain("No vehicles yet");
    expect(empty).not.toContain("ingest");
    expect(screen.queryByRole("radio")).toBeNull();
  });
});
