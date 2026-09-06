// Adding a vehicle: the form validates before it sends, and past loads are visible rather than silent.

describe("adding and loading a vehicle", () => {
  beforeEach(() => {
    cy.visit("/");
  });

  it("offers a form rather than telling anyone to run a command", () => {
    cy.get('[data-testid="add-vehicle-open"]').realClick();
    cy.get('[data-testid="add-vehicle-form"]').should("exist");
    cy.get('input[aria-label="NHTSA model"]').should("exist");
    cy.get('input[aria-label="Your name for it"]').should("exist");
  });

  it("refuses an incomplete registration without sending it", () => {
    // Catching this in the page saves a round trip and a confusing server-side rejection.
    cy.get('[data-testid="add-vehicle-open"]').realClick();
    cy.get('input[aria-label="Your name for it"]').realClick();
    cy.get('input[aria-label="Your name for it"]').realType("A car with no model");
    cy.get('[data-testid="add-vehicle-submit"]').realClick();

    cy.get('[data-testid="add-vehicle-problem"]').should("contain.text", "model name");
    cy.get('[data-testid="add-vehicle-form"]').should("exist");
  });

  it("closes the form again without registering anything", () => {
    cy.get('[data-testid="add-vehicle-open"]').realClick();
    cy.get('[data-testid="add-vehicle-cancel"]').realClick();

    cy.get('[data-testid="add-vehicle-form"]').should("not.exist");
    cy.get('[data-testid="add-vehicle-open"]').should("exist");
  });

  it("shows past loads, including a scheduled one that failed", () => {
    // A refresh that failed overnight has to be findable, or the records quietly go stale.
    cy.get('[data-testid="recent-loads-toggle"]').realClick();
    cy.get('[data-testid="recent-loads"]').should("exist");
    cy.get('[data-testid="recent-loads"]').should("contain.text", "succeeded");
    cy.get('[data-testid="recent-loads"] [data-state="failed"]')
      .should("exist")
      .and("contain.text", "NHTSA returned 500");
  });

  it("names who started each load, so a scheduled refresh is distinguishable", () => {
    cy.get('[data-testid="recent-loads-toggle"]').realClick();

    cy.get('[data-testid="recent-loads"]').should("contain.text", "manual");
    cy.get('[data-testid="recent-loads"]').should("contain.text", "scheduled");
  });
});
