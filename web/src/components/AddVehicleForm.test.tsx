// Checks the add-vehicle form validates before sending and surfaces NHTSA's own rejection.
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { AddVehicleForm, EarliestModelYear, findProblem, latestModelYear } from "./AddVehicleForm";
import { ApiError } from "../api/client";
import { createStubClient } from "../api/stubClient";
import type { ApiClient, Load, RegisterVehicleRequest } from "../api/client";

const queued: Load = {
  id: 12,
  vehicleId: null,
  displayName: "2013 Explorer Sport",
  state: "queued",
  trigger: "manual",
  isFinished: false,
  queuedAt: "2026-09-06T12:00:00Z",
  startedAt: null,
  finishedAt: null,
  message: null,
  report: null,
};

function createClient(registerVehicle: (request: RegisterVehicleRequest) => Promise<Load>): ApiClient {
  return createStubClient({ registerVehicle });
}

function openAndFill(values: { model?: string; name?: string; year?: string } = {}) {
  fireEvent.click(screen.getByTestId("add-vehicle-open"));
  fireEvent.change(screen.getByLabelText("NHTSA model"), { target: { value: values.model ?? "EXPLORER" } });
  fireEvent.change(screen.getByLabelText("Your name for it"), { target: { value: values.name ?? "2013 Explorer Sport" } });
  fireEvent.change(screen.getByLabelText("Model year"), { target: { value: values.year ?? "2013" } });
}

describe("AddVehicleForm", () => {
  it("sends the registration and hands back the load that was started", async () => {
    const sent: RegisterVehicleRequest[] = [];
    render(
      <AddVehicleForm
        client={createClient((request) => {
          sent.push(request);
          return Promise.resolve(queued);
        })}
        onLoadStarted={() => undefined}
      />,
    );

    openAndFill();
    fireEvent.submit(screen.getByTestId("add-vehicle-form"));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0].nhtsaModel).toBe("EXPLORER");
    expect(sent[0].modelYear).toBe(2013);
  });

  it("reports the load to its caller so the page can show progress", async () => {
    const started: Load[] = [];
    render(<AddVehicleForm client={createClient(() => Promise.resolve(queued))} onLoadStarted={(load) => started.push(load)} />);

    openAndFill();
    fireEvent.submit(screen.getByTestId("add-vehicle-form"));

    await waitFor(() => expect(started).toEqual([queued]));
  });

  it("shows NHTSA's rejection, which names the model strings it does know", async () => {
    // A bare "invalid" would leave someone stuck; the list of valid names is the useful part.
    const detail = "NHTSA has no complaint model named 'EXPLORRER'. Known names include: EXPLORER, EDGE.";
    render(
      <AddVehicleForm
        client={createClient(() => Promise.reject(new ApiError(400, "Unknown NHTSA model", detail)))}
        onLoadStarted={() => undefined}
      />,
    );

    openAndFill({ model: "EXPLORRER" });
    fireEvent.submit(screen.getByTestId("add-vehicle-form"));

    await waitFor(() => expect(screen.getByTestId("add-vehicle-problem").textContent).toContain("EXPLORER"));
  });

  it("refuses to send a registration that could never be looked up", async () => {
    let callCount = 0;
    render(
      <AddVehicleForm
        client={createClient(() => {
          callCount += 1;
          return Promise.resolve(queued);
        })}
        onLoadStarted={() => undefined}
      />,
    );

    openAndFill({ model: "" });
    fireEvent.submit(screen.getByTestId("add-vehicle-form"));

    await waitFor(() => expect(screen.getByTestId("add-vehicle-problem")).toBeTruthy());
    expect(callCount).toBe(0);
  });

  it("names what is wrong before anything is sent", () => {
    const base: RegisterVehicleRequest = { make: "Ford", nhtsaModel: "EXPLORER", modelYear: 2013, displayName: "Mine" };
    const now = new Date("2026-09-06T00:00:00Z");

    expect(findProblem(base, now)).toBeNull();
    expect(findProblem({ ...base, make: "  " }, now)).toContain("make");
    expect(findProblem({ ...base, nhtsaModel: "" }, now)).toContain("model name");
    expect(findProblem({ ...base, displayName: "" }, now)).toContain("name is required");
    expect(findProblem({ ...base, modelYear: EarliestModelYear - 1 }, now)).toContain("model year");
    expect(findProblem({ ...base, modelYear: latestModelYear(now) + 1 }, now)).toContain("model year");
  });

  it("offers the names NHTSA actually files under, so nobody has to guess the vocabulary", async () => {
    // A Mach-E is filed as "MUSTANG MACH-E BEV BEV". Nobody would type that unprompted.
    const client = createStubClient({
      registerVehicle: () => Promise.resolve(queued),
      listNhtsaModels: () => Promise.resolve(["MUSTANG ICE", "MUSTANG MACH-E BEV BEV"]),
    });
    render(<AddVehicleForm client={client} onLoadStarted={() => undefined} />);

    fireEvent.click(screen.getByTestId("add-vehicle-open"));

    await waitFor(() => expect(screen.getByTestId("model-count").textContent).toContain("2 names on file"));
    const options = screen.getByTestId("nhtsa-model-names").querySelectorAll("option");
    expect([...options].map((option) => option.getAttribute("value"))).toContain("MUSTANG MACH-E BEV BEV");
  });

  it("asks for the names of the make and year actually entered", async () => {
    const asked: { make: string; year: number }[] = [];
    const client = createStubClient({
      registerVehicle: () => Promise.resolve(queued),
      listNhtsaModels: (make, modelYear) => {
        asked.push({ make, year: modelYear });
        return Promise.resolve([]);
      },
    });
    render(<AddVehicleForm client={client} onLoadStarted={() => undefined} />);

    fireEvent.click(screen.getByTestId("add-vehicle-open"));
    fireEvent.change(screen.getByLabelText("Model year"), { target: { value: "2026" } });

    await waitFor(() => expect(asked.some((entry) => entry.year === 2026 && entry.make === "Ford")).toBe(true));
  });

  it("still lets the name be typed when the list cannot be fetched", async () => {
    // The list is a convenience. Losing it must not block registering a vehicle.
    const sent: RegisterVehicleRequest[] = [];
    const client = createStubClient({
      registerVehicle: (request) => {
        sent.push(request);
        return Promise.resolve(queued);
      },
      listNhtsaModels: () => Promise.reject(new Error("offline")),
    });
    render(<AddVehicleForm client={client} onLoadStarted={() => undefined} />);

    openAndFill();
    fireEvent.submit(screen.getByTestId("add-vehicle-form"));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(screen.queryByTestId("model-count")).toBeNull();
  });
});
