// Checks the eval page loads the report on mount and handles the no-runs and error states.
import { render, screen } from "@testing-library/react";
import type { ApiClient, EvalReport } from "../api/client";
import { EvalPage } from "./EvalPage";
import { createStubClient } from "../api/stubClient";

const report: EvalReport = {
  latest: { id: 3, ranAt: "2026-09-03T18:00:00Z", caseCount: 41, metrics: { sparse: { recallAt5: 0.5, recallAt10: 0.6, mrr: 0.4 }, faithfulness: { emitted: 10, verified: 9 } } },
  history: [{ id: 1, ranAt: "2026-09-01T18:00:00Z", caseCount: 41, metrics: {} }, { id: 2, ranAt: "2026-09-02T18:00:00Z", caseCount: 41, metrics: {} }],
};

function createClient(getEval: () => Promise<EvalReport>): ApiClient {
  return createStubClient({ getEval });
}

describe("EvalPage", () => {
  it("renders the latest run and counts the history", async () => {
    render(<EvalPage client={createClient(() => Promise.resolve(report))} />);

    await screen.findByTestId("eval-table");
    expect(screen.getByTestId("eval-row-sparse").textContent).toContain("0.500");
    expect(screen.getByTestId("eval-history-count").textContent).toBe("2 earlier runs recorded.");
  });

  it("explains that no run exists yet", async () => {
    render(<EvalPage client={createClient(() => Promise.resolve({ latest: null, history: [] }))} />);

    expect((await screen.findByTestId("eval-empty")).textContent).toContain("No evaluation has run yet");
  });

  it("shows the failure when the report cannot load", async () => {
    render(<EvalPage client={createClient(() => Promise.reject(new Error("database down")))} />);

    expect((await screen.findByTestId("eval-error")).textContent).toContain("database down");
  });
});
