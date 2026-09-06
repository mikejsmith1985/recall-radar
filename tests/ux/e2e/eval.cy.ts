// Evaluation view: both pools are shown, with recall and MRR per mode and the faithfulness figure.

describe("evaluation page", () => {
  beforeEach(() => {
    cy.visit("/");
    cy.get('[data-testid="tab-eval"]').realClick();
    cy.get('[data-testid="eval-table"]').should("exist");
  });

  it("shows recall and MRR per mode plus faithfulness for the latest run", () => {
    cy.get('[data-testid="eval-row-sparse"]').should("exist");
    cy.get('[data-testid="eval-row-dense"]').should("exist");
    cy.get('[data-testid="eval-row-hybrid"]').should("exist");
    cy.get('[data-testid="eval-pool-all"] thead')
      .should("contain.text", "Recall@5")
      .and("contain.text", "Recall@10")
      .and("contain.text", "MRR");
    cy.get('[data-testid="faithfulness"]').should("contain.text", "Citation faithfulness");
  });

  it("shows the campaign pool beside the wider one, so the two can be compared", () => {
    // The pools disagreeing is the finding. A single table would hide it.
    cy.get('[data-testid="eval-pool-all"]').should("exist");
    cy.get('[data-testid="eval-pool-campaigns"]').should("exist");
    cy.get('[data-testid="eval-row-campaigns-sparse"]').should("exist");
  });

  it("reads a different number for the same mode in each pool", () => {
    cy.get('[data-testid="eval-row-sparse"]')
      .invoke("text")
      .then((widePoolRow) => {
        cy.get('[data-testid="eval-row-campaigns-sparse"]').invoke("text").should("not.equal", widePoolRow);
      });
  });

  it("marks a mode the run could not score rather than printing zero", () => {
    // A zero would read as "dense is bad" when it means "dense was never tried".
    cy.get('[data-testid="eval-row-campaigns-dense"]').should("contain.text", "skipped");
  });
});
