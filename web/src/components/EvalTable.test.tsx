// Checks the eval table shows every mode's numbers, marks skipped modes, and reports faithfulness.
import { render, screen } from "@testing-library/react";
import { EvalTable, formatFaithfulness } from "./EvalTable";
import type { EvalRun } from "../api/client";

const run: EvalRun = {
  id: 3,
  ranAt: "2026-09-03T18:00:00Z",
  caseCount: 41,
  metrics: {
    dense: { recallAt5: 0.5, recallAt10: 0.7, mrr: 0.41 },
    sparse: { recallAt5: 0.55, recallAt10: 0.72, mrr: 0.44 },
    hybrid: { recallAt5: 0.62, recallAt10: 0.8, mrr: 0.5 },
    faithfulness: { emitted: 60, verified: 58 },
  },
};

describe("EvalTable", () => {
  it("renders one row per mode with three decimals", () => {
    render(<EvalTable run={run} />);

    expect(screen.getByTestId("eval-row-dense").textContent).toContain("0.500");
    expect(screen.getByTestId("eval-row-sparse").textContent).toContain("0.720");
    expect(screen.getByTestId("eval-row-hybrid").textContent).toContain("0.500");
    expect(screen.getByTestId("eval-table").textContent).toContain("41 ground-truth cases");
  });

  it("shows faithfulness as verified over emitted with a percentage", () => {
    render(<EvalTable run={run} />);

    expect(screen.getByTestId("faithfulness").textContent).toContain("58 / 60 (96.7%)");
    expect(formatFaithfulness(0, 0)).toBe("no citations emitted");
  });

  it("marks modes the run could not score and unmeasured faithfulness", () => {
    render(<EvalTable run={{ ...run, metrics: { sparse: run.metrics.sparse } }} />);

    expect(screen.getByTestId("eval-row-dense").textContent).toContain("skipped");
    expect(screen.getByTestId("eval-row-hybrid").textContent).toContain("skipped");
    expect(screen.getByTestId("faithfulness").textContent).toContain("not measured");
  });
});
