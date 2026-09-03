// Loads real-event support, and refuses to run against an empty Recall Radar.
//
// cypress-real-events dispatches through the Chrome DevTools Protocol, so a
// click is a real browser click rather than a synthetic DOM event. Article V
// requires this: a synthetic event can pass against markup a person could not
// actually operate.
//
// The fixture check below exists because a suite that cannot fail is worse than
// no suite: it is believed. Every spec here needs at least one vehicle with
// loaded complaints, so the run stops before the first spec if there is none.
import "cypress-real-events";

interface VehicleSummary {
  id: number;
  displayName: string;
  counts: { complaint: number; recall: number; investigation: number };
}

const MinimumComplaintsPerVehicle = 1;

before(() => {
  cy.request<VehicleSummary[]>("/api/vehicles").then((response) => {
    const loadedVehicles = response.body.filter((vehicle) => vehicle.counts.complaint >= MinimumComplaintsPerVehicle);
    expect(
      loadedVehicles.length,
      "at least one vehicle with loaded complaints is required before this suite means anything — " +
        "run it through `run-dev-clean.ps1 -CypressOnly`, which seeds a fixture database",
    ).to.be.at.least(1);
  });
});
