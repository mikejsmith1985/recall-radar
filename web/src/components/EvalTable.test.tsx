// Checks the eval table shows every mode's numbers, marks skipped modes, and reports faithfulness.
import { render, screen } from "@testing-library/react";
import { EvalTable, formatFaithfulness } from "./EvalTable";
import { metricsKey } from "../api/client";
import type { EvalRun } from "../api/client";

const run: EvalRun = {
  id: 3,
  ranAt: "2026-09-03T18:00:00Z",
  caseCount: 41,
  metrics: {
    dense: { recallAt5: 0.5, recallAt10: 0.7, mrr: 0.41 },
    sparse: { recallAt5: 0.55, recallAt10: 0.72, mrr: 0.44 },
    hybrid: { recallAt5: 0.62, recallAt10: 0.8, mrr: 0.5 },
    campaignsDense: { recallAt5: 0.11, recallAt10: 0.22, mrr: 0.09 },
    campaignsSparse: { recallAt5: 0.33, recallAt10: 0.44, mrr: 0.28 },
    campaignsHybrid: { recallAt5: 0.55, recallAt10: 0.66, mrr: 0.51 },
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

  it("renders both pools, so the two sets of numbers can be compared", () => {
    render(<EvalTable run={run} />);

    expect(screen.getByTestId("eval-pool-all")).toBeTruthy();
    expect(screen.getByTestId("eval-pool-campaigns")).toBeTruthy();
    expect(screen.getByTestId("eval-row-campaigns-sparse").textContent).toContain("0.440");
    expect(screen.getByTestId("eval-row-campaigns-hybrid").textContent).toContain("0.660");
  });

  it("keeps the two pools' rows distinct rather than overwriting one with the other", () => {
    render(<EvalTable run={run} />);

    expect(screen.getByTestId("eval-row-sparse").textContent).toContain("0.720");
    expect(screen.getByTestId("eval-row-campaigns-sparse").textContent).not.toContain("0.720");
  });

  it("marks a campaign-pool mode that could not be scored", () => {
    render(<EvalTable run={{ ...run, metrics: { sparse: run.metrics.sparse } }} />);

    expect(screen.getByTestId("eval-row-campaigns-dense").textContent).toContain("skipped");
  });

  it("names the metrics key each mode occupies in each pool", () => {
    expect(metricsKey("sparse", "all")).toBe("sparse");
    expect(metricsKey("sparse", "campaigns")).toBe("campaignsSparse");
    expect(metricsKey("hybrid", "campaigns")).toBe("campaignsHybrid");
  });
});
