// Grounded answers: ask about a symptom, see the verified citations and the dropped count,
// and open a citation to find the quoted span highlighted inside the source record.

const Question = "exhaust smell inside the cabin";

function selectFirstVehicle() {
  cy.get('[data-testid^="vehicle-option-"]').first().realClick();
}

describe("ask about a symptom", () => {
  beforeEach(() => {
    cy.visit("/");
    selectFirstVehicle();
    cy.get('[data-testid="tab-ask"]').realClick();
    cy.get('[data-testid="ask-page"]').should("exist");
  });

  it("answers with verified citations, shows the dropped count, and highlights an opened citation", () => {
    cy.get('[data-testid="answering-unavailable"]').should("not.exist");
    cy.get('input[aria-label="Symptom"]').realClick();
    cy.get('input[aria-label="Symptom"]').realType(Question);
    cy.get('[data-testid="search-box"] button[type="submit"]').realClick();

    cy.get('[data-testid="answer-panel"]', { timeout: 60000 }).should("exist");
    cy.get('[data-testid="grounded-status"]').should("have.text", "Grounded");
    cy.get('[data-testid="dropped-count"]').should("have.attr", "data-dropped");
    cy.get('[data-testid="citation-button"]').should("have.length.at.least", 1);

    cy.get('[data-testid="citation-button"]').first().find("blockquote").invoke("text").then((quote) => {
      cy.get('[data-testid="citation-button"]').first().realClick();
      cy.get('[data-testid="citation-view"]').should("exist");
      cy.get('[data-testid="citation-highlight"]').should("have.text", quote);
    });
  });

  it("shows the recall and investigation the campaign pool found, marked as unverified", () => {
    // The fixture's answer quotes a complaint only. Without its own pool the recall and the
    // investigation are outranked by complaints and the owner never learns they exist.
    cy.get('input[aria-label="Symptom"]').realClick();
    cy.get('input[aria-label="Symptom"]').realType(Question);
    cy.get('[data-testid="search-box"] button[type="submit"]').realClick();

    cy.get('[data-testid="answer-panel"]', { timeout: 60000 }).should("exist");
    cy.get('[data-testid="uncited-matches"]').should("exist");
    cy.get('[data-testid="uncited-matches"]').should("contain.text", "EA17002");
    cy.get('[data-testid="uncited-caption"]').should("contain.text", "none of it has been verified");
  });

  it("keeps a quoted record out of the unverified list", () => {
    // A record cannot be both proven evidence and an unverified lead. Showing it twice would
    // blur the only distinction this page exists to make.
    cy.get('input[aria-label="Symptom"]').realClick();
    cy.get('input[aria-label="Symptom"]').realType(Question);
    cy.get('[data-testid="search-box"] button[type="submit"]').realClick();

    cy.get('[data-testid="answer-panel"]', { timeout: 60000 }).should("exist");
    cy.get('[data-testid="citation-button"]').first().find("blockquote").should("exist");
    cy.get('[data-testid="uncited-matches"]').should("not.contain.text", "11257832");
  });
});
