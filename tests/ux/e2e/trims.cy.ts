// Narrowing a search to the owner's own trim, and opening it back up when that is too narrow.
//
// NHTSA files every version of a model under one name, so a search that does not narrow is
// answering about a different vehicle. The fixture vehicle carries a VIN and one complaint from
// another engine, which is what these specs move in and out of scope.

describe("searching one trim", () => {
  beforeEach(() => {
    cy.visit("/");
    cy.get('[data-testid="vehicle-option-1"]').should("exist");
  });

  it("offers the vehicle's own trim by name, not a generic label", () => {
    // Somebody has to recognise their own truck in the button.
    cy.get('[data-testid="trims-thisTrim"]').should("contain.text", "Sport");
    cy.get('[data-testid="trims-thisTrim"]').should("have.attr", "aria-checked", "true");
  });

  it("says nothing about other versions until a search has counted them", () => {
    // Zero would be a claim, and nothing has counted yet.
    cy.get('[data-testid="trim-explanation"]').should("not.exist");
  });

  it("says how many records belong to other versions once a search has run", () => {
    cy.get('input[aria-label="Search phrase"]').realClick();
    cy.get('input[aria-label="Search phrase"]').realType("exhaust odor cabin");
    cy.get('[data-testid="search-box"]').submit();

    cy.get('[data-testid="trim-explanation"]').should("contain.text", "another version");
  });

  it("leaves out a record from another engine, and brings it back on request", () => {
    cy.get('input[aria-label="Search phrase"]').realClick();
    cy.get('input[aria-label="Search phrase"]').realType("brake pedal master cylinder");
    cy.get('[data-testid="search-box"]').submit();

    // The brake complaint belongs to a 2.0 four-cylinder, and this vehicle is a 3.5 six.
    cy.get('[data-testid="result-card"]').should("not.exist");

    cy.get('[data-testid="trims-allTrims"]').realClick();

    cy.get('[data-testid="result-card"]').should("have.length.at.least", 1);
    cy.get('[data-testid="result-card"]').first().find('[data-testid="hit-trim"]').should("contain.text", "Base");
  });

  it("labels every record with the version it was filed under", () => {
    cy.get('input[aria-label="Search phrase"]').realClick();
    cy.get('input[aria-label="Search phrase"]').realType("exhaust odor cabin");
    cy.get('[data-testid="search-box"]').submit();

    cy.get('[data-testid="result-card"]').should("have.length.at.least", 1);
    cy.get('[data-testid="hit-trim"]').first().should("not.be.empty");
  });
});
