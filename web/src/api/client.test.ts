// Checks that the client builds the URLs and bodies the API contract documents, and surfaces problems as ApiError.
import { ApiError, buildSearchQuery, createApiClient } from "./client";

interface RecordedCall {
  url: string;
  init?: RequestInit;
}

function createFakeFetch(status: number, body: unknown) {
  const calls: RecordedCall[] = [];
  const fetchImpl = (url: string, init?: RequestInit) => {
    calls.push({ url, init });
    return Promise.resolve(
      new Response(JSON.stringify(body), {
        status,
        headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
      }),
    );
  };
  return { calls, fetchImpl };
}

describe("buildSearchQuery", () => {
  it("includes only the filters that were supplied", () => {
    const query = buildSearchQuery({ vehicleId: 1, query: "exhaust smell", mode: "hybrid" });

    expect(query).toBe("vehicleId=1&q=exhaust+smell&mode=hybrid");
  });

  it("passes component, date range and limit through by their contract names", () => {
    const query = buildSearchQuery({
      vehicleId: 2,
      query: "steering",
      mode: "sparse",
      component: "STEERING",
      filedFrom: "2019-01-01",
      filedTo: "2019-12-31",
      limit: 25,
    });

    expect(query).toBe("vehicleId=2&q=steering&mode=sparse&component=STEERING&filedFrom=2019-01-01&filedTo=2019-12-31&limit=25");
  });
});

describe("createApiClient", () => {
  it("reads health from the root health endpoint", async () => {
    const { calls, fetchImpl } = createFakeFetch(200, { status: "ok", database: "ok", embeddings: "unavailable", answering: "ok" });

    const health = await createApiClient(fetchImpl).getHealth();

    expect(calls[0].url).toBe("/health");
    expect(health.embeddings).toBe("unavailable");
  });

  it("lists vehicles from /api/vehicles", async () => {
    const vehicles = [{ id: 1, displayName: "2013 Explorer Sport", make: "FORD", modelYear: 2013, counts: { complaint: 2231, recall: 12, investigation: 4 } }];
    const { calls, fetchImpl } = createFakeFetch(200, vehicles);

    const listed = await createApiClient(fetchImpl, "http://api").listVehicles();

    expect(calls[0].url).toBe("http://api/api/vehicles");
    expect(listed).toEqual(vehicles);
  });

  it("searches with the documented query string", async () => {
    const { calls, fetchImpl } = createFakeFetch(200, { mode: "sparse", hits: [] });

    await createApiClient(fetchImpl).search({ vehicleId: 1, query: "exhaust", mode: "sparse" });

    expect(calls[0].url).toBe("/api/search?vehicleId=1&q=exhaust&mode=sparse");
  });

  it("posts the ask body as JSON", async () => {
    const { calls, fetchImpl } = createFakeFetch(200, { answerId: 1, answer: "", isKnownPattern: false, isGrounded: false, citations: [], droppedCitationCount: 0, linkedCampaigns: [], retrievedDocumentIds: [] });

    await createApiClient(fetchImpl).ask({ vehicleId: 1, question: "exhaust smell", mode: "hybrid" });

    expect(calls[0].url).toBe("/api/ask");
    expect(calls[0].init?.method).toBe("POST");
    expect(JSON.parse(String(calls[0].init?.body))).toEqual({ vehicleId: 1, question: "exhaust smell", mode: "hybrid" });
  });

  it("fetches a document by id and the eval report", async () => {
    const { calls, fetchImpl } = createFakeFetch(200, { latest: null, history: [] });
    const client = createApiClient(fetchImpl);

    await client.getEval();
    await client.getDocument(4123);

    expect(calls[0].url).toBe("/api/eval");
    expect(calls[1].url).toBe("/api/documents/4123");
  });

  it("turns a problem response into an ApiError that knows about missing embeddings", async () => {
    const { fetchImpl } = createFakeFetch(409, { title: "Conflict", detail: "Embeddings are unavailable; use mode=sparse" });

    const failure = await createApiClient(fetchImpl)
      .search({ vehicleId: 1, query: "exhaust", mode: "dense" })
      .catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    const apiError = failure as ApiError;
    expect(apiError.status).toBe(409);
    expect(apiError.isEmbeddingsUnavailable).toBe(true);
    expect(apiError.message).toBe("Embeddings are unavailable; use mode=sparse");
  });

  it("still raises an ApiError when the error body is not JSON", async () => {
    const fetchImpl = () => Promise.resolve(new Response("gateway timeout", { status: 504, statusText: "Gateway Timeout" }));

    const failure = await createApiClient(fetchImpl).listVehicles().catch((error: unknown) => error);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).status).toBe(504);
  });
});
