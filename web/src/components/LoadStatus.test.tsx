// Checks the load view polls while a load runs, stops when it ends, and shows a failure's reason.
import { render, screen, waitFor } from "@testing-library/react";
import { describeState, LoadStatus, summariseReport } from "./LoadStatus";
import type { ApiClient, Load } from "../api/client";

const running: Load = {
  id: 12,
  vehicleId: null,
  displayName: "2013 Explorer Sport",
  state: "running",
  trigger: "manual",
  isFinished: false,
  queuedAt: "2026-09-06T12:00:00Z",
  startedAt: "2026-09-06T12:00:02Z",
  finishedAt: null,
  message: null,
  report: null,
};

const succeeded: Load = {
  ...running,
  vehicleId: 3,
  state: "succeeded",
  isFinished: true,
  finishedAt: "2026-09-06T12:03:00Z",
  report: { complaintsNew: 2231, recallsNew: 12, investigationsNew: 6, chunksCreated: 2249, chunksEmbedded: 2249 },
};

function createClient(getLoad: (id: number) => Promise<Load>): ApiClient {
  const unsupported = () => Promise.reject(new Error("not used in this test"));
  return {
    getHealth: unsupported,
    listVehicles: unsupported,
    search: unsupported,
    ask: unsupported,
    getDocument: unsupported,
    getEval: unsupported,
    registerVehicle: unsupported,
    refreshVehicle: unsupported,
    getLoad,
    listLoads: unsupported,
  };
}

describe("LoadStatus", () => {
  beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
  afterEach(() => vi.useRealTimers());

  it("says what is happening in words somebody watching would use", () => {
    expect(describeState(running)).toBe("Fetching from NHTSA…");
    expect(describeState({ ...running, state: "queued" })).toBe("Waiting to start");
    expect(describeState(succeeded)).toBe("Loaded");
    expect(describeState({ ...running, state: "failed" })).toBe("Could not load");
  });

  it("polls until the load finishes, then tells its caller", async () => {
    let calls = 0;
    const finished: Load[] = [];
    render(
      <LoadStatus
        client={createClient(() => {
          calls += 1;
          return Promise.resolve(succeeded);
        })}
        load={running}
        onFinished={(load) => finished.push(load)}
      />,
    );

    await vi.advanceTimersByTimeAsync(2500);

    await waitFor(() => expect(finished).toHaveLength(1));
    expect(calls).toBeGreaterThan(0);
    expect(finished[0].state).toBe("succeeded");
  });

  it("stops polling once the load has finished, rather than asking forever", async () => {
    let calls = 0;
    render(
      <LoadStatus
        client={createClient(() => {
          calls += 1;
          return Promise.resolve(succeeded);
        })}
        load={succeeded}
        onFinished={() => undefined}
      />,
    );

    await vi.advanceTimersByTimeAsync(10000);

    expect(calls).toBe(0);
  });

  it("keeps watching when one poll fails, because a failed request is not a failed load", async () => {
    let calls = 0;
    render(
      <LoadStatus
        client={createClient(() => {
          calls += 1;
          return calls === 1 ? Promise.reject(new Error("network")) : Promise.resolve(succeeded);
        })}
        load={running}
        onFinished={() => undefined}
      />,
    );

    await vi.advanceTimersByTimeAsync(5000);

    expect(calls).toBeGreaterThan(1);
  });

  it("shows the counts a finished load produced", async () => {
    render(<LoadStatus client={createClient(() => Promise.resolve(succeeded))} load={succeeded} onFinished={() => undefined} />);

    const counts = screen.getByTestId("load-counts").textContent ?? "";
    expect(counts).toContain("2231 complaints");
    expect(counts).toContain("6 investigations");
  });

  it("shows why a load failed, because a silent failure looks like one still running", () => {
    const failed: Load = { ...running, state: "failed", isFinished: true, message: "NHTSA returned 500." };

    render(<LoadStatus client={createClient(() => Promise.resolve(failed))} load={failed} onFinished={() => undefined} />);

    expect(screen.getByTestId("load-message").textContent).toContain("500");
    expect(screen.getByTestId("load-status").getAttribute("data-state")).toBe("failed");
  });

  it("skips counts a load never reported rather than printing zero", () => {
    // A field an older version never wrote is unknown, and zero would read as a measurement.
    const partial: Load = { ...succeeded, report: { complaintsNew: 5 } };

    expect(summariseReport(partial)).toEqual([{ label: "complaints", value: 5 }]);
    expect(summariseReport(running)).toEqual([]);
  });
});
