// Evaluation view: the table shows every retrieval mode and the citation faithfulness figure.

describe("evaluation page", () => {
  it("shows recall and MRR per mode plus faithfulness for the latest run", () => {
    cy.visit("/");
    cy.get('[data-testid="tab-eval"]').realClick();

    cy.get('[data-testid="eval-table"]').should("exist");
    cy.get('[data-testid="eval-row-sparse"]').should("exist");
    cy.get('[data-testid="eval-row-dense"]').should("exist");
    cy.get('[data-testid="eval-row-hybrid"]').should("exist");
    cy.get('[data-testid="eval-table"] thead').should("contain.text", "Recall@5").and("contain.text", "Recall@10").and("contain.text", "MRR");
    cy.get('[data-testid="faithfulness"]').should("contain.text", "Citation faithfulness");
  });
});
