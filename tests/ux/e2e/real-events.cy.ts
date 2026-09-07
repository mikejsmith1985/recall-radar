// The suite's own foundation: a real click has to land on the thing it was aimed at.
//
// Every other spec here trusts that realClick reaches its target. It once did not, silently: a
// screenshot leaves Cypress's application frame un-scaled for a moment, and cypress-real-events
// works out a whole-tab coordinate from that frame's scale and offset. A click computed at scale 1
// instead of 0.772 landed 340 pixels away, on the page header, and nothing reported an error —
// because a click did happen. These tests would catch that returning.

interface ClickRecord {
  x: number;
  y: number;
  testId: string | null;
}

/** Records where clicks actually land, so a failure says what was hit instead of what was missed. */
function recordClicks(records: ClickRecord[]) {
  cy.window().then((win) => {
    win.document.addEventListener(
      "click",
      (event) => {
        const target = event.target as HTMLElement;
        records.push({ x: event.clientX, y: event.clientY, testId: target.getAttribute("data-testid") });
      },
      true,
    );
  });
}

function expectClickOn(records: ClickRecord[], testId: string) {
  cy.wrap(records, { log: false }).should((hits: ClickRecord[]) => {
    const last = hits[hits.length - 1];
    expect(last, "a click was received at all").to.not.equal(undefined);
    expect(last.testId, `the click landed at (${last?.x}, ${last?.y})`).to.equal(testId);
  });
}

describe("real events", () => {
  beforeEach(() => {
    cy.visit("/");
    cy.get('[data-testid="add-vehicle-open"]').should("exist");
  });

  it("lands on the element it was aimed at", () => {
    const records: ClickRecord[] = [];
    recordClicks(records);

    cy.get('[data-testid="add-vehicle-open"]').realClick();

    expectClickOn(records, "add-vehicle-open");
    cy.get('[data-testid="add-vehicle-form"]').should("exist");
  });

  it("still lands on it straight after a screenshot", () => {
    const records: ClickRecord[] = [];
    recordClicks(records);
    cy.screenshot("real-events-frame-check", { capture: "viewport", overwrite: true });

    cy.get('[data-testid="add-vehicle-open"]').realClick();

    expectClickOn(records, "add-vehicle-open");
    cy.get('[data-testid="add-vehicle-form"]').should("exist");
  });

  it("still lands on it after the viewport changes", () => {
    const records: ClickRecord[] = [];
    recordClicks(records);
    cy.viewport(1280, 900);

    cy.get('[data-testid="add-vehicle-open"]').realClick();

    expectClickOn(records, "add-vehicle-open");
    cy.get('[data-testid="add-vehicle-form"]').should("exist");
  });
});
