// Checks the search page drives the client with the chosen mode and falls back to keyword mode on 409.
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError, type ApiClient, type SearchParams, type SearchResponse } from "../api/client";
import { SearchPage } from "./SearchPage";
import { createStubClient } from "../api/stubClient";

const hit = { documentId: 1, chunkId: 1, kind: "complaint" as const, externalId: "11760888", title: "", component: "STEERING", filedOn: "2019-01-02", snippet: "Steering locked", denseRank: null, sparseRank: 1, fusedScore: 0.0164, trim: null };

function createClient(search: (params: SearchParams) => Promise<SearchResponse>): ApiClient {
  return createStubClient({ search });
}

function submitQuery(text: string) {
  fireEvent.change(screen.getByLabelText("Search phrase"), { target: { value: text } });
  fireEvent.submit(screen.getByTestId("search-box"));
}

describe("SearchPage", () => {
  it("asks for a vehicle before searching", () => {
    render(<SearchPage client={createClient(() => Promise.reject(new Error()))} vehicleId={null} isEmbeddingsAvailable vehicleTrim={null} />);

    expect(screen.getByTestId("search-needs-vehicle")).toBeTruthy();
  });

  it("says what the page is for before anybody has searched", () => {
    render(<SearchPage client={createClient(() => Promise.reject(new Error()))} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);

    expect(screen.getByTestId("search-invitation")).toBeTruthy();
  });

  it("stops inviting once there are results to read", async () => {
    const client = createClient((params) => Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const, trims: "allTrims" as const, otherTrims: 0 }));
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);

    submitQuery("steering locks");

    await screen.findByTestId("result-card");
    expect(screen.queryByTestId("search-invitation")).toBeNull();
  });

  it("searches the selected vehicle in combined mode and lists the hits", async () => {
    const requests: SearchParams[] = [];
    const client = createClient((params) => {
      requests.push(params);
      return Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const, trims: "allTrims" as const, otherTrims: 0 });
    });
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);

    submitQuery("steering locks");

    await screen.findByTestId("result-card");
    // The narrow scope is the default the page asks for; the server ignores it for a vehicle whose
    // VIN has not been decoded, so nothing is silently lost by asking.
    expect(requests).toEqual([{ vehicleId: 7, query: "steering locks", mode: "hybrid", trims: "thisTrim" }]);
  });

  it("moves to the combined default once health says meaning search is available", async () => {
    // Health answers after this page mounts. Sampling it once at mount left the page in keyword
    // mode on a server that could do every mode, which is what the browser suite caught.
    const client = createClient((params) => Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const, trims: "allTrims" as const, otherTrims: 0 }));
    const { rerender } = render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable={false} vehicleTrim={null} />);
    expect(screen.getByTestId("mode-sparse").getAttribute("aria-checked")).toBe("true");

    rerender(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);

    await waitFor(() => expect(screen.getByTestId("mode-hybrid").getAttribute("aria-checked")).toBe("true"));
  });

  it("leaves a mode somebody picked alone when health arrives afterwards", async () => {
    const client = createClient((params) => Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const, trims: "allTrims" as const, otherTrims: 0 }));
    const { rerender } = render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);
    fireEvent.click(screen.getByTestId("mode-sparse"));

    rerender(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);

    await waitFor(() => expect(screen.getByTestId("mode-sparse").getAttribute("aria-checked")).toBe("true"));
  });

  it("re-runs the last query when the mode changes", async () => {
    const requests: SearchParams[] = [];
    const client = createClient((params) => {
      requests.push(params);
      return Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const, trims: "allTrims" as const, otherTrims: 0 });
    });
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);
    submitQuery("steering locks");
    await screen.findByTestId("result-card");

    fireEvent.click(screen.getByTestId("mode-sparse"));

    await waitFor(() => expect(requests).toHaveLength(2));
    expect(requests[1].mode).toBe("sparse");
    expect(screen.getByTestId("result-list").getAttribute("data-mode")).toBe("sparse");
  });

  it("falls back to keyword mode and shows the reason when embeddings are unavailable", async () => {
    const client = createClient(() => Promise.reject(new ApiError(409, "Conflict", "Embeddings are unavailable; use mode=sparse")));
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable vehicleTrim={null} />);

    submitQuery("steering locks");

    expect((await screen.findByTestId("search-error")).textContent).toContain("Embeddings are unavailable");
    expect(screen.getByTestId("mode-sparse").getAttribute("aria-checked")).toBe("true");
  });

  it("starts in keyword mode and says nothing matched when the list is empty", async () => {
    const requests: SearchParams[] = [];
    const client = createClient((params) => {
      requests.push(params);
      return Promise.resolve({ mode: params.mode, hits: [], scope: "all" as const, trims: "allTrims" as const, otherTrims: 0 });
    });
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable={false} vehicleTrim={null} />);

    submitQuery("unicorn horn");

    expect((await screen.findByTestId("search-empty")).textContent).toContain("unicorn horn");
    expect(requests[0].mode).toBe("sparse");
  });
});
