// Checks the shell loads vehicles and health, selects the first vehicle, and switches views.
import { fireEvent, render, screen } from "@testing-library/react";
import { App } from "./App";
import type { ApiClient, HealthReport, Vehicle } from "./api/client";
import { createStubClient } from "./api/stubClient";

const vehicles: Vehicle[] = [
  { id: 1, displayName: "2013 Explorer Sport", make: "FORD", modelYear: 2013, counts: { complaint: 2231, recall: 12, investigation: 4 } },
];

function createClient(health: HealthReport, listVehicles: () => Promise<Vehicle[]>): ApiClient {
  return createStubClient({
    getHealth: () => Promise.resolve(health),
    listVehicles,
    getEval: () => Promise.resolve({ latest: null, history: [] }),
    listLoads: () => Promise.resolve([]),
  });
}

const healthy: HealthReport = { status: "ok", database: "ok", embeddings: "ok", answering: "ok" };

describe("App", () => {
  it("loads vehicles, selects the first, and starts on search", async () => {
    render(<App client={createClient(healthy, () => Promise.resolve(vehicles))} />);

    const option = await screen.findByTestId("vehicle-option-1");
    expect(option.getAttribute("aria-checked")).toBe("true");
    expect(screen.getByTestId("search-page")).toBeTruthy();
    expect(screen.getByTestId("tab-search").getAttribute("aria-selected")).toBe("true");
  });

  it("switches to the ask and evaluation views", async () => {
    render(<App client={createClient(healthy, () => Promise.resolve(vehicles))} />);
    await screen.findByTestId("vehicle-option-1");

    fireEvent.click(screen.getByTestId("tab-ask"));
    expect(screen.getByTestId("ask-page")).toBeTruthy();

    fireEvent.click(screen.getByTestId("tab-eval"));
    expect(await screen.findByTestId("eval-empty")).toBeTruthy();
  });

  it("passes the health capabilities down so modes reflect what the API can serve", async () => {
    const degraded: HealthReport = { ...healthy, embeddings: "unavailable" };
    render(<App client={createClient(degraded, () => Promise.resolve(vehicles))} />);
    await screen.findByTestId("vehicle-option-1");

    expect((await screen.findByTestId("mode-dense")) as HTMLButtonElement).toBeTruthy();
    expect((screen.getByTestId("mode-dense") as HTMLButtonElement).disabled).toBe(true);
  });

  it("says so when the database is behind the server it belongs to", async () => {
    // This state reached a user as a bare "Internal Server Error" on the add-vehicle form, because
    // health had reported it all along and nothing on the page read the field.
    const behind: HealthReport = { ...healthy, status: "unavailable", database: "schema-outdated" };
    render(<App client={createClient(behind, () => Promise.resolve(vehicles))} />);

    expect((await screen.findByTestId("database-warning")).textContent).toContain("behind this build");
  });

  it("says so when the database cannot be reached at all", async () => {
    const unreachable: HealthReport = { ...healthy, status: "unavailable", database: "unavailable" };
    render(<App client={createClient(unreachable, () => Promise.resolve(vehicles))} />);

    expect((await screen.findByTestId("database-warning")).textContent).toContain("cannot reach its database");
  });

  it("stays quiet while health has not answered yet", async () => {
    // "unknown" is the state before the first response, not a fault worth alarming anybody about.
    render(<App client={createClient(healthy, () => Promise.resolve(vehicles))} />);
    await screen.findByTestId("vehicle-option-1");

    expect(screen.queryByTestId("database-warning")).toBeNull();
  });

  it("shows the failure when vehicles cannot load", async () => {
    render(<App client={createClient(healthy, () => Promise.reject(new Error("database unreachable")))} />);

    expect((await screen.findByTestId("vehicle-load-error")).textContent).toContain("database unreachable");
  });
});
