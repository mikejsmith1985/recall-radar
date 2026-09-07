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

/**
 * Cypress shows the application inside a scaled-down frame, and cypress-real-events turns an
 * element's position into a whole-tab coordinate using that frame's current scale and offset.
 *
 * `cy.screenshot()` un-scales the frame to capture the page at full size, and restores it on a later
 * render rather than before it resolves. A real click in between is computed at scale 1 against
 * offset (0, 0) instead of 0.772 against (466, 80), and lands somewhere else entirely: measured at
 * (561, 125) for a button whose centre was (899, 215), which is the page header. Nothing reports an
 * error, because a click did happen -- just not where it was aimed.
 *
 * The un-scaled state is recognisable: Cypress puts the frame at the top-left corner to capture it,
 * and its own chrome means it is never there otherwise. Every real event that needs a coordinate
 * waits for the frame to leave that corner. When the frame is where it belongs, which is every case
 * but this one, the check passes on its first read and costs nothing.
 */
interface FrameGeometry {
  x: number;
  y: number;
}

const CaptureCorner: FrameGeometry = { x: 0, y: 0 };

function measureApplicationFrame(): FrameGeometry | null {
  const frame = window.parent.document.querySelector("iframe");
  if (!frame) {
    return null;
  }
  const { x, y } = frame.getBoundingClientRect();
  return { x, y };
}

function isBeingCaptured(frame: FrameGeometry | null): boolean {
  return frame !== null && frame.x === CaptureCorner.x && frame.y === CaptureCorner.y;
}

/**
 * Retries until the application frame is back where real-event coordinates are calculated from.
 * A frame that never leaves the corner fails this assertion by timing out, which is the right
 * outcome: a loud failure beats a click that silently lands on the wrong element.
 */
function whenTheApplicationFrameIsPlaced(): Cypress.Chainable<null> {
  return cy.wrap(null, { log: false }).should(() => {
    expect(
      isBeingCaptured(measureApplicationFrame()),
      "the application frame has been put back after a screenshot",
    ).to.equal(false);
  });
}

// Only the commands that turn a position into a coordinate. Keyboard commands carry no geometry.
const PositionedRealEventCommands = [
  "realClick",
  "realHover",
  "realMouseDown",
  "realMouseUp",
  "realMouseMove",
  "realSwipe",
  "realTouch",
] as const;

for (const commandName of PositionedRealEventCommands) {
  Cypress.Commands.overwrite(
    commandName as Parameters<typeof Cypress.Commands.overwrite>[0],
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    (originalFn: any, ...args: unknown[]) =>
      whenTheApplicationFrameIsPlaced().then(() => originalFn(...args)) as unknown as void,
  );
}

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
