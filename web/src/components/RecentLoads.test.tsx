// Checks the recent-loads table makes an overnight failure visible rather than silent.
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { chooseWhen, describeWhen, RecentLoads, VisibleLoadCount } from "./RecentLoads";
import { createStubClient } from "../api/stubClient";
import type { ApiClient, Load } from "../api/client";

const succeeded: Load = {
  id: 1,
  vehicleId: 3,
  displayName: "2013 Explorer Sport",
  state: "succeeded",
  trigger: "manual",
  isFinished: true,
  queuedAt: "2026-09-06T12:00:00Z",
  startedAt: "2026-09-06T12:00:02Z",
  finishedAt: "2026-09-06T12:03:00Z",
  message: null,
  report: { complaintsNew: 2231 },
};

const failed: Load = {
  ...succeeded,
  id: 2,
  state: "failed",
  trigger: "scheduled",
  message: "NHTSA returned 500.",
  report: null,
};

function createClient(listLoads: () => Promise<Load[]>): ApiClient {
  return createStubClient({ listLoads });
}

describe("RecentLoads", () => {
  it("lists each load with who started it", async () => {
    render(<RecentLoads client={createClient(() => Promise.resolve([succeeded, failed]))} refreshToken={0} />);

    await waitFor(() => expect(screen.getByTestId("recent-loads")).toBeTruthy());
    expect(screen.getByTestId("recent-load-1").textContent).toContain("succeeded");
    expect(screen.getByTestId("recent-load-2").textContent).toContain("scheduled");
  });

  it("shows why a scheduled refresh failed, so an overnight failure is not silent", async () => {
    render(<RecentLoads client={createClient(() => Promise.resolve([failed]))} refreshToken={0} />);

    await waitFor(() => expect(screen.getByTestId("recent-load-2").textContent).toContain("NHTSA returned 500."));
    expect(screen.getByTestId("recent-load-2").getAttribute("data-state")).toBe("failed");
    // Not the page's alert box: inside a table cell it burst out of the row it belongs to.
    expect(screen.getByTestId("recent-load-2").querySelector(".error-state")).toBeNull();
    expect(screen.getByTestId("recent-load-2").querySelector(".failure-note")).toBeTruthy();
  });

  it("dismisses a load and takes its row away", async () => {
    const dismissed: number[] = [];
    const client = createStubClient({
      listLoads: () => Promise.resolve([succeeded, failed]),
      dismissLoad: (loadId: number) => {
        dismissed.push(loadId);
        return Promise.resolve();
      },
    });
    render(<RecentLoads client={client} refreshToken={0} />);
    await screen.findByTestId("recent-load-2");

    fireEvent.click(screen.getByTestId("dismiss-load-2"));

    await waitFor(() => expect(screen.queryByTestId("recent-load-2")).toBeNull());
    expect(dismissed).toEqual([2]);
    expect(screen.getByTestId("recent-load-1")).toBeTruthy();
  });

  it("offers no dismissal for a load that has not finished", async () => {
    // The runner claims jobs by row, so one still going is the server's to remove, not this table's.
    const running: Load = { ...succeeded, id: 5, state: "running", isFinished: false, finishedAt: null };
    render(<RecentLoads client={createClient(() => Promise.resolve([running]))} refreshToken={0} />);
    await screen.findByTestId("recent-load-5");

    expect(screen.queryByTestId("dismiss-load-5")).toBeNull();
  });

  it("puts the row back and says why when the server refuses, without hiding the history", async () => {
    const client = createStubClient({
      listLoads: () => Promise.resolve([failed]),
      dismissLoad: () => Promise.reject(new Error("Load 2 has not finished.")),
    });
    render(<RecentLoads client={client} refreshToken={0} />);
    await screen.findByTestId("recent-load-2");

    fireEvent.click(screen.getByTestId("dismiss-load-2"));

    await waitFor(() => expect(screen.getByTestId("dismiss-error").textContent).toContain("has not finished"));
    expect(screen.getByTestId("recent-load-2")).toBeTruthy();
  });

  it("names the load in the button, so the choice is unambiguous without the row", async () => {
    render(<RecentLoads client={createClient(() => Promise.resolve([failed]))} refreshToken={0} />);
    await screen.findByTestId("recent-load-2");

    expect(screen.getByTestId("dismiss-load-2").getAttribute("aria-label"))
      .toBe("Dismiss the failed load of 2013 Explorer Sport");
  });

  it("says so when nothing has been loaded through the app", async () => {
    render(<RecentLoads client={createClient(() => Promise.resolve([]))} refreshToken={0} />);

    await waitFor(() => expect(screen.getByTestId("recent-loads-empty")).toBeTruthy());
  });

  it("shows an error rather than an empty table when the list cannot be read", async () => {
    // An empty table would read as "nothing has ever been loaded", which is a different fact.
    render(<RecentLoads client={createClient(() => Promise.reject(new Error("offline")))} refreshToken={0} />);

    await waitFor(() => expect(screen.getByTestId("recent-loads-error").textContent).toContain("offline"));
  });

  it("keeps the list short enough to read at a glance", async () => {
    const many = Array.from({ length: VisibleLoadCount + 4 }, (_unused, index) => ({ ...succeeded, id: index + 10 }));

    render(<RecentLoads client={createClient(() => Promise.resolve(many))} refreshToken={0} />);

    await waitFor(() => expect(screen.getByTestId("recent-loads")).toBeTruthy());
    expect(screen.getByTestId("recent-loads").querySelectorAll("tbody tr")).toHaveLength(VisibleLoadCount);
  });

  it("re-reads when a load finishes, because the list is stale the moment one does", async () => {
    let calls = 0;
    const client = createClient(() => {
      calls += 1;
      return Promise.resolve([succeeded]);
    });
    const { rerender } = render(<RecentLoads client={client} refreshToken={0} />);
    await waitFor(() => expect(calls).toBe(1));

    rerender(<RecentLoads client={client} refreshToken={1} />);

    await waitFor(() => expect(calls).toBe(2));
  });

  it("dates a load by when it ended, falling back while it has not", () => {
    expect(chooseWhen(succeeded)).toBe(succeeded.finishedAt);
    expect(chooseWhen({ ...succeeded, finishedAt: null })).toBe(succeeded.startedAt);
    expect(chooseWhen({ ...succeeded, finishedAt: null, startedAt: null })).toBe(succeeded.queuedAt);
  });

  it("shows a date somebody can read rather than the wire format", () => {
    // The server sends ISO 8601 with microseconds and an offset, which is right on the wire and
    // unreadable in a table.
    const shown = describeWhen(succeeded);

    expect(shown).not.toContain("T");
    expect(shown).not.toContain(succeeded.finishedAt);
    expect(shown.length).toBeGreaterThan(0);
  });

  it("shows a timestamp it cannot read unchanged, rather than hiding it", () => {
    // A date nobody can format is still evidence that something upstream is wrong.
    expect(describeWhen({ ...succeeded, finishedAt: "not a date" })).toBe("not a date");
  });
});
