// Typed client for the Recall Radar API. Every endpoint in specs/001-recall-radar/contracts/http-api.md
// has one function here, so the rest of the UI never builds a URL or reads raw JSON.

export type RetrievalMode = "dense" | "sparse" | "hybrid";
/** Which pool of records a search ranks within. Campaigns are recalls and investigations only. */
export type RetrievalScope = "all" | "campaigns";
export type SourceKind = "complaint" | "recall" | "investigation";

export interface HealthReport {
  status: string;
  database: string;
  embeddings: "ok" | "unavailable";
  answering: "ok" | "unavailable";
}

export interface VehicleCounts {
  complaint: number;
  recall: number;
  investigation: number;
}

export interface Vehicle {
  id: number;
  displayName: string;
  make: string;
  modelYear: number;
  counts: VehicleCounts;
}

export interface SearchParams {
  vehicleId: number;
  query: string;
  mode: RetrievalMode;
  component?: string;
  filedFrom?: string;
  filedTo?: string;
  limit?: number;
}

export interface SearchHit {
  documentId: number;
  chunkId: number;
  kind: SourceKind;
  externalId: string;
  title: string;
  component: string;
  filedOn: string | null;
  snippet: string;
  denseRank: number | null;
  sparseRank: number | null;
  fusedScore: number;
}

export interface SearchResponse {
  mode: RetrievalMode;
  hits: SearchHit[];
  scope: RetrievalScope;
}

export interface AskRequest {
  vehicleId: number;
  question: string;
  mode: RetrievalMode;
}

export interface Citation {
  documentId: number;
  externalId: string;
  kind: SourceKind;
  quote: string;
  startOffset: number;
  endOffset: number;
}

/**
 * A recall or investigation the campaign pool matched. Retrieved, not quoted: it is something
 * to read, never evidence for the answer. Only a verified citation is evidence.
 */
export interface CampaignMatch {
  documentId: number;
  kind: SourceKind;
  externalId: string;
  title: string;
  component: string;
  filedOn: string | null;
}

export interface AskResponse {
  answerId: number;
  answer: string;
  isKnownPattern: boolean;
  isGrounded: boolean;
  citations: Citation[];
  droppedCitationCount: number;
  linkedCampaigns: string[];
  retrievedDocumentIds: number[];
  campaignMatches: CampaignMatch[];
}

export interface SourceDocument {
  id: number;
  kind: SourceKind;
  externalId: string;
  title: string;
  component: string;
  filedOn: string | null;
  body: string;
  vehicleId: number;
}

/** Per-mode retrieval quality. A mode the run could not score (no embeddings) is absent or null. */
export interface ModeMetrics {
  recallAt5: number;
  recallAt10: number;
  mrr: number;
}

export interface FaithfulnessMetrics {
  emitted: number;
  verified: number;
}

export interface EvalMetrics {
  dense?: ModeMetrics | null;
  sparse?: ModeMetrics | null;
  hybrid?: ModeMetrics | null;
  campaignsDense?: ModeMetrics | null;
  campaignsSparse?: ModeMetrics | null;
  campaignsHybrid?: ModeMetrics | null;
  faithfulness?: FaithfulnessMetrics | null;
}

/** The key one mode occupies in a run's metrics, for a given pool. */
export function metricsKey(mode: RetrievalMode, scope: RetrievalScope): keyof EvalMetrics {
  if (scope === "all") {
    return mode;
  }
  return `campaigns${mode.charAt(0).toUpperCase()}${mode.slice(1)}` as keyof EvalMetrics;
}

export interface EvalRun {
  id: number;
  ranAt: string;
  caseCount: number;
  metrics: EvalMetrics;
}

export interface EvalReport {
  latest: EvalRun | null;
  history: EvalRun[];
}

/** Raised for any non-2xx response, carrying the RFC 9457 problem fields the API returns. */
export class ApiError extends Error {
  public readonly status: number;
  public readonly title: string;
  public readonly detail: string;

  constructor(status: number, title: string, detail: string) {
    super(detail || title || `Request failed with status ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.title = title;
    this.detail = detail;
  }

  /** The API answers 409 when dense or hybrid mode is requested without embeddings. */
  get isEmbeddingsUnavailable(): boolean {
    return this.status === 409;
  }
}

/** How far a load has got. It only ever moves forwards through these. */
export type LoadState = "queued" | "running" | "succeeded" | "failed";

/** What a load produced. Absent until it succeeds, and shaped by the version that wrote it. */
export interface LoadReport {
  complaintsNew?: number;
  complaintsUnchanged?: number;
  recallsNew?: number;
  investigationsNew?: number;
  linksCreated?: number;
  chunksCreated?: number;
  chunksEmbedded?: number;
}

/** One load of a vehicle's NHTSA records: where it has got to, and what it produced. */
export interface Load {
  id: number;
  vehicleId: number | null;
  displayName: string;
  state: LoadState;
  trigger: "manual" | "scheduled";
  isFinished: boolean;
  queuedAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  message: string | null;
  report: LoadReport | null;
}

/** A vehicle as the owner describes it, with the NHTSA names its records are filed under. */
export interface RegisterVehicleRequest {
  make: string;
  nhtsaModel: string;
  recallModel?: string | null;
  modelYear: number;
  displayName: string;
}

export interface ApiClient {
  getHealth(): Promise<HealthReport>;
  listVehicles(): Promise<Vehicle[]>;
  registerVehicle(request: RegisterVehicleRequest): Promise<Load>;
  listNhtsaModels(make: string, modelYear: number): Promise<string[]>;
  refreshVehicle(vehicleId: number): Promise<Load>;
  getLoad(loadId: number): Promise<Load>;
  listLoads(): Promise<Load[]>;
  /** Removes a finished load from the history. Rejects for one still queued or running. */
  dismissLoad(loadId: number): Promise<void>;
  search(params: SearchParams): Promise<SearchResponse>;
  ask(request: AskRequest): Promise<AskResponse>;
  getDocument(documentId: number): Promise<SourceDocument>;
  getEval(): Promise<EvalReport>;
}

type FetchLike = (input: string, init?: RequestInit) => Promise<Response>;

const jsonHeaders = { "Content-Type": "application/json", Accept: "application/json" };

/** Builds the query string for GET /api/search, omitting filters that were not supplied. */
export function buildSearchQuery(params: SearchParams): string {
  const query = new URLSearchParams();
  query.set("vehicleId", String(params.vehicleId));
  query.set("q", params.query);
  query.set("mode", params.mode);
  if (params.component) {
    query.set("component", params.component);
  }
  if (params.filedFrom) {
    query.set("filedFrom", params.filedFrom);
  }
  if (params.filedTo) {
    query.set("filedTo", params.filedTo);
  }
  if (params.limit !== undefined) {
    query.set("limit", String(params.limit));
  }
  return query.toString();
}

async function readProblem(response: Response): Promise<ApiError> {
  const fallbackTitle = response.statusText || "Request failed";
  try {
    const problem = (await response.json()) as { title?: string; detail?: string };
    return new ApiError(response.status, problem.title ?? fallbackTitle, problem.detail ?? "");
  } catch {
    return new ApiError(response.status, fallbackTitle, "");
  }
}

/** For a response whose success carries no body. A refusal still has to reach the caller. */
async function readNothing(response: Response): Promise<void> {
  if (!response.ok) {
    throw await readProblem(response);
  }
}

async function readJson<TBody>(response: Response): Promise<TBody> {
  if (!response.ok) {
    throw await readProblem(response);
  }
  return (await response.json()) as TBody;
}

/**
 * Creates the client. `fetchImpl` is injected so tests can hand in a fake and the browser uses
 * `window.fetch`; `baseUrl` is empty in the app because the API serves the page.
 */
export function createApiClient(fetchImpl: FetchLike, baseUrl = ""): ApiClient {
  return {
    getHealth: () => fetchImpl(`${baseUrl}/health`, { headers: jsonHeaders }).then(readJson<HealthReport>),

    listVehicles: () => fetchImpl(`${baseUrl}/api/vehicles`, { headers: jsonHeaders }).then(readJson<Vehicle[]>),

    listNhtsaModels: (make, modelYear) =>
      fetchImpl(`${baseUrl}/api/nhtsa/models?make=${encodeURIComponent(make)}&modelYear=${modelYear}`, {
        headers: jsonHeaders,
      }).then(readJson<string[]>),

    registerVehicle: (request) =>
      fetchImpl(`${baseUrl}/api/vehicles`, {
        method: "POST",
        headers: jsonHeaders,
        body: JSON.stringify(request),
      }).then(readJson<Load>),

    refreshVehicle: (vehicleId) =>
      fetchImpl(`${baseUrl}/api/vehicles/${vehicleId}/refresh`, {
        method: "POST",
        headers: jsonHeaders,
      }).then(readJson<Load>),

    getLoad: (loadId) =>
      fetchImpl(`${baseUrl}/api/loads/${loadId}`, { headers: jsonHeaders }).then(readJson<Load>),

    listLoads: () => fetchImpl(`${baseUrl}/api/loads`, { headers: jsonHeaders }).then(readJson<Load[]>),

    // 204, so there is no body to read: only whether the server refused.
    dismissLoad: (loadId) =>
      fetchImpl(`${baseUrl}/api/loads/${loadId}`, { method: "DELETE", headers: jsonHeaders }).then(readNothing),

    search: (params) =>
      fetchImpl(`${baseUrl}/api/search?${buildSearchQuery(params)}`, { headers: jsonHeaders }).then(
        readJson<SearchResponse>,
      ),

    ask: (request) =>
      fetchImpl(`${baseUrl}/api/ask`, {
        method: "POST",
        headers: jsonHeaders,
        body: JSON.stringify(request),
      }).then(readJson<AskResponse>),

    getDocument: (documentId) =>
      fetchImpl(`${baseUrl}/api/documents/${documentId}`, { headers: jsonHeaders }).then(readJson<SourceDocument>),

    getEval: () => fetchImpl(`${baseUrl}/api/eval`, { headers: jsonHeaders }).then(readJson<EvalReport>),
  };
}
