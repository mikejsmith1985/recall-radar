import { defineConfig } from "cypress";

// UX tests drive the real Recall Radar UI with real pointer and keyboard events.
// Constitution Article V forbids synthetic events, which is why every spec uses
// cypress-real-events (realClick, realType) rather than cy.click() or cy.type().
// The API under test is started by scripts/run-dev-clean.ps1 -CypressOnly on this port.
export default defineConfig({
  e2e: {
    baseUrl: "http://127.0.0.1:5180",
    specPattern: "e2e/**/*.cy.ts",
    supportFile: "support/e2e.ts",
    video: false,
    screenshotOnRunFailure: true,
    defaultCommandTimeout: 8000,
  },
});
