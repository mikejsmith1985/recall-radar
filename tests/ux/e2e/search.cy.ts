// Explainable search: pick a vehicle, search, and see how each record was ranked.
// Every interaction is a real pointer or keyboard event.

interface HealthReport {
  embeddings: "ok" | "unavailable";
}

const SearchPhrase = "exhaust odor";

function selectFirstVehicle() {
  cy.get('[data-testid^="vehicle-option-"]').first().realClick();
  cy.get('[data-testid^="vehicle-option-"]').first().should("have.attr", "aria-checked", "true");
}

function searchFor(phrase: string) {
  cy.get('input[aria-label="Search phrase"]').realClick();
  cy.get('input[aria-label="Search phrase"]').realType(phrase);
  cy.get('[data-testid="search-box"] button[type="submit"]').realClick();
}

describe("search with rank explanations", () => {
  beforeEach(() => {
    cy.visit("/");
    selectFirstVehicle();
  });

  it("lists matching records, each with meaning rank, keyword rank and fused score", () => {
    searchFor(SearchPhrase);

    cy.get('[data-testid="result-card"]').should("have.length.at.least", 1);
    cy.get('[data-testid="result-card"]').first().within(() => {
      cy.get('[data-testid="dense-rank"]').should("exist");
      cy.get('[data-testid="sparse-rank"]').should("contain.text", "Keyword");
      cy.get('[data-testid="fused-score"]').should("contain.text", "Fused score");
    });
  });

  it("reflects what the API can serve: keyword mode always works, meaning modes only with embeddings", () => {
    cy.request<HealthReport>("/health").then((health) => {
      const hasEmbeddings = health.body.embeddings === "ok";

      searchFor(SearchPhrase);
      cy.get('[data-testid="result-card"]').should("have.length.at.least", 1);

      if (!hasEmbeddings) {
        cy.get('[data-testid="mode-dense"]').should("be.disabled");
        cy.get('[data-testid="mode-hybrid"]').should("be.disabled");
        cy.get('[data-testid="mode-explanation"]').should("contain.text", "no embedding key");
        cy.get('[data-testid="result-list"]').should("have.attr", "data-mode", "sparse");
        return;
      }

      cy.get('[data-testid="result-list"]').should("have.attr", "data-mode", "hybrid");
      cy.get('[data-testid="result-card"]').first().invoke("attr", "data-external-id").then((hybridTop) => {
        cy.get('[data-testid="mode-sparse"]').realClick();
        cy.get('[data-testid="result-list"]').should("have.attr", "data-mode", "sparse");
        cy.get('[data-testid="result-card"]').should("have.length.at.least", 1);
        cy.get('[data-testid="mode-dense"]').realClick();
        cy.get('[data-testid="result-list"]').should("have.attr", "data-mode", "dense");
        cy.get('[data-testid="result-card"]').first().find('[data-testid="dense-rank"]').should("contain.text", "rank 1");
        cy.log(`hybrid top result was ${hybridTop}`);
      });
    });
  });
});
