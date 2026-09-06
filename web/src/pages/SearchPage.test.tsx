// Checks the search page drives the client with the chosen mode and falls back to keyword mode on 409.
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { ApiError, type ApiClient, type SearchParams, type SearchResponse } from "../api/client";
import { SearchPage } from "./SearchPage";
import { createStubClient } from "../api/stubClient";

const hit = { documentId: 1, chunkId: 1, kind: "complaint" as const, externalId: "11760888", title: "", component: "STEERING", filedOn: "2019-01-02", snippet: "Steering locked", denseRank: null, sparseRank: 1, fusedScore: 0.0164 };

function createClient(search: (params: SearchParams) => Promise<SearchResponse>): ApiClient {
  return createStubClient({ search });
}

function submitQuery(text: string) {
  fireEvent.change(screen.getByLabelText("Search phrase"), { target: { value: text } });
  fireEvent.submit(screen.getByTestId("search-box"));
}

describe("SearchPage", () => {
  it("asks for a vehicle before searching", () => {
    render(<SearchPage client={createClient(() => Promise.reject(new Error()))} vehicleId={null} isEmbeddingsAvailable />);

    expect(screen.getByTestId("search-needs-vehicle")).toBeTruthy();
  });

  it("searches the selected vehicle in combined mode and lists the hits", async () => {
    const requests: SearchParams[] = [];
    const client = createClient((params) => {
      requests.push(params);
      return Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const });
    });
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable />);

    submitQuery("steering locks");

    await screen.findByTestId("result-card");
    expect(requests).toEqual([{ vehicleId: 7, query: "steering locks", mode: "hybrid" }]);
  });

  it("re-runs the last query when the mode changes", async () => {
    const requests: SearchParams[] = [];
    const client = createClient((params) => {
      requests.push(params);
      return Promise.resolve({ mode: params.mode, hits: [hit], scope: "all" as const });
    });
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable />);
    submitQuery("steering locks");
    await screen.findByTestId("result-card");

    fireEvent.click(screen.getByTestId("mode-sparse"));

    await waitFor(() => expect(requests).toHaveLength(2));
    expect(requests[1].mode).toBe("sparse");
    expect(screen.getByTestId("result-list").getAttribute("data-mode")).toBe("sparse");
  });

  it("falls back to keyword mode and shows the reason when embeddings are unavailable", async () => {
    const client = createClient(() => Promise.reject(new ApiError(409, "Conflict", "Embeddings are unavailable; use mode=sparse")));
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable />);

    submitQuery("steering locks");

    expect((await screen.findByTestId("search-error")).textContent).toContain("Embeddings are unavailable");
    expect(screen.getByTestId("mode-sparse").getAttribute("aria-checked")).toBe("true");
  });

  it("starts in keyword mode and says nothing matched when the list is empty", async () => {
    const requests: SearchParams[] = [];
    const client = createClient((params) => {
      requests.push(params);
      return Promise.resolve({ mode: params.mode, hits: [], scope: "all" as const });
    });
    render(<SearchPage client={client} vehicleId={7} isEmbeddingsAvailable={false} />);

    submitQuery("unicorn horn");

    expect((await screen.findByTestId("search-empty")).textContent).toContain("unicorn horn");
    expect(requests[0].mode).toBe("sparse");
  });
});
