# Research: Recall Radar

**Date**: 2026-09-03 | **Plan**: [plan.md](plan.md)

Every Technical Context entry was resolvable from the approved brief and from probes run against the
live NHTSA endpoints and package feeds on 2026-09-03. No `NEEDS CLARIFICATION` remains.

## R1 — Data sources and their shapes

**Decision**: Three NHTSA sources, all read-only, all public, no key.

| Source | Endpoint | Verified facts (2026-09-03) |
|---|---|---|
| Complaints | `GET https://api.nhtsa.gov/complaints/complaintsByVehicle?make=ford&model=<M>&modelYear=<Y>` | 2013 Explorer: 2,231 rows. 2014 F-150: 1,362 rows, but only under model strings `F-150 SUPER CREW`, `F-150 SUPERCAB`, `F-150 REGULAR CAB` (bare `F-150` returns 0); all three return the identical set → dedupe on `odiNumber`. Fields: `odiNumber, manufacturer, crash, fire, numberOfInjuries, numberOfDeaths, dateOfIncident, dateComplaintFiled, vin, components, summary, products`. Summary length min 18 / avg 527 / max ~2,000 chars. Dates are `MM/DD/YYYY`. |
| Recalls | `GET https://api.nhtsa.gov/recalls/recallsByVehicle?make=ford&model=<M>&modelYear=<Y>` | 2013 Explorer: 12; 2014 F-150: 8 (bare `F-150` works here). Fields: `NHTSACampaignNumber, Component, Summary, Consequence, Remedy, ReportReceivedDate, ModelYear, Make, Model, parkIt, parkOutSide, overTheAirUpdate, Notes`. Note one row carried `ReportReceivedDate` as `25/07/2017` (day-first) — parse both orders, prefer month-first, log the ambiguity. |
| Investigations | `GET https://static.nhtsa.gov/odi/ffdd/inv/FLAT_INV.zip` | 4.3 MB zip containing `FLAT_INV.txt`, ~154k tab-separated rows, 11 columns, Latin-1: `action_no, make, model, year, component, manufacturer, open_date(YYYYMMDD), close_date, campaign_no, subject, summary`. 31 rows match our two vehicles. `campaign_no` is the recall link. EA17002 "Exhaust Odor in Passenger Cab" appears for 2013 and 2014 Explorer under two components. One investigation spans several rows (one per make/model/year/component) → collapse by `action_no` per vehicle. |
| Model names | `GET https://api.nhtsa.gov/products/vehicle/models?modelYear=<Y>&make=ford&issueType=c` | Returns the exact model strings the complaints endpoint accepts. Used to validate a vehicle at registration. |

**Rationale**: The complaints API returns everything in one response (no paging observed at 2,231
rows), so ingest is one call per model string. The investigations flat file is the only source that
links to campaigns, which is what makes the evaluation honest.

**Alternatives considered**: NHTSA TSB flat file (`FLAT_TSBS.zip`) — returns 404 as of today; out of
scope. Recalls flat file (`FLAT_RCL.zip`) — also 404; the recalls API suffices. Scraping
`nhtsa.gov` pages — rejected, the APIs are official and stable.

## R2 — Embeddings provider (framework-first)

**Decision**: `Microsoft.Extensions.AI.Abstractions` `IEmbeddingGenerator<string, Embedding<float>>`
as the seam; production implementation is a `VoyageEmbeddingGenerator` over `HttpClient`
(`POST https://api.voyageai.com/v1/embeddings`, model `voyage-3.5`, 1024 dimensions, batches ≤ 128
inputs); a `DeterministicEmbeddingGenerator` (stable hash → unit vector) for unit and integration
tests.

**Rationale**: Anthropic does not offer an embeddings endpoint and recommends Voyage. The framework
interface already exists, so the custom surface is the HTTP adapter only.

**Drift justification (Article VII)**: Voyage ships no C# SDK and no `Microsoft.Extensions.AI`
provider package; the adapter is the minimum custom piece behind the framework seam.

**Key pending**: `VOYAGE_API_KEY` is not yet in the vault. Until it is, the API reports
`embeddings: unavailable`, `/api/search` accepts `mode=sparse` only and returns HTTP 409 with a
reason for `dense`/`hybrid`, ingest stores chunks with `embedding IS NULL`, and a later
`ingest embed` verb back-fills. Nothing else blocks.

**Alternatives considered**: Gemini embeddings (key already in vault) — viable fallback via the same
interface; not chosen by default to keep the Anthropic-recommended pairing. Local ONNX/Ollama — adds an
install and a second runtime for no portfolio gain here.

## R3 — Storage and indexing

**Decision**: PostgreSQL 17 + `vector` extension through EF Core 10 (`Npgsql.EntityFrameworkCore.PostgreSQL`
10.0.3, `Pgvector.EntityFrameworkCore` 0.3.0). `document_chunks.embedding vector(1024)` with an HNSW
index using `vector_cosine_ops`; `document_chunks.search_text tsvector GENERATED ALWAYS AS
(to_tsvector('english', text)) STORED` with a GIN index. Local dev via `docker-compose.yml`
(`pgvector/pgvector:pg17`, host port 5433); integration tests start their own container.

**Rationale**: One database gives vectors, full-text and structured filters in a single SQL statement,
which is exactly what the hybrid query needs and what the project is meant to teach.

**Alternatives considered**: SQLite + sqlite-vec — no native full-text ranking comparable to
`ts_rank_cd`, and Testcontainers would add nothing. Dedicated vector DB — hides the fusion.

## R4 — Hybrid retrieval and fusion (framework-first)

**Decision**: Hand-written SQL executed through EF Core `SqlQueryRaw`/`FromSql`:

1. Filter: `vehicle_id = @vehicle` [`AND component = @component`] [`AND filed_on BETWEEN ...`].
2. Dense candidates: `ORDER BY embedding <=> @query LIMIT 50`.
3. Sparse candidates: `WHERE search_text @@ websearch_to_tsquery('english', @q) ORDER BY ts_rank_cd(search_text, websearch_to_tsquery('english', @q)) DESC LIMIT 50`.
4. Fusion in `RecallRadar.Domain.Retrieval.ReciprocalRankFusion` with `k = 60`: `score = Σ 1/(k + rank)`.
   Pure function over two ordered id lists → deterministic, unit-testable.
5. Each hit returns `denseRank`, `sparseRank` (nullable) and `fusedScore`.

**Drift justification (Article VII)**: `Microsoft.Extensions.VectorData` returns a single score per
hit and no keyword rank; FR-007 requires per-method ranks.

**Alternatives considered**: `pg_search`/ParadeDB BM25 — extra extension, not in the pgvector image;
`ts_rank_cd` is adequate for the corpus size. Weighted score blending instead of RRF — needs
per-corpus tuning; RRF is parameter-light and well-documented.

## R5 — Grounded answering and verification

**Decision**: `Anthropic` C# SDK 12.45, model `claude-opus-5`, adaptive thinking (default), streaming
off (answer ≤ ~1,500 tokens), `OutputConfig.Format` JSON schema:

```json
{ "answer": string, "isKnownPattern": boolean,
  "citations": [{ "documentId": string, "quote": string }],
  "linkedCampaigns": [string] }
```

System prompt states that every quote is checked character-for-character against the record and a
failed quote discards that citation. Verification is a port of Randomness `policy/verify.py`:
collapse all whitespace runs to one space and trim on both sides, then ordinal `Contains` against
`source_documents.body`. Failures are dropped and counted (`droppedCitationCount`); zero survivors →
`isGrounded = false`. `documentId` must be one of the ids actually sent in the prompt.

**Refusal handling**: `stop_reason == "refusal"` is treated as "not grounded" with the category logged.
Server-side fallbacks are not enabled: the model is Opus, not Fable, and a refused safety-defect
question should surface, not be silently re-routed.

**Alternatives considered**: Tool use to force a schema — rejected, structured output is the documented
path and forced `tool_choice` is going away on newer models. Fuzzy quote matching — rejected by
design; a paraphrase is exactly what must fail.

## R6 — Test layers (Article V)

**Decision**:

| Layer | Tooling | Rules |
|---|---|---|
| Unit | xUnit 2.9, NSubstitute 6; `UnitTestBudgetFixture` records `Stopwatch` per test and fails the collection if any exceeds 10 ms | No I/O. Domain tests need no doubles; Retrieval/Api tests substitute `IEmbeddingGenerator`, `IAnthropicClient`-shaped seams and the DbContext via in-memory query stubs |
| Integration | `Testcontainers.PostgreSql` 4.14 on `pgvector/pgvector:pg17`; `WireMock.Net` 2.15 in-process server for NHTSA, Voyage and Anthropic with recorded JSON fixtures | Real migrations run against the container; no live network |
| UX | Cypress 13 + `cypress-real-events`; `support/e2e.ts` `before()` fails if the seeded API reports zero records; launched only by `scripts/run-dev-clean.ps1 -CypressOnly` against a seeded fixture database | Never `cy.click()`; `realClick`/`realType` only |
| Web unit | Vitest + Testing Library for components | Satisfies the pre-commit test-file gate for `.tsx` |

**Rationale**: Mirrors Randomness exactly, including the lesson that a suite which cannot fail is
worse than none.

## R7 — Process control and secrets

**Decision**: `scripts/run-dev-clean.ps1` ported verbatim in structure; PID file `.recall-radar.pid`;
`Start-App` runs `dotnet run --project src/RecallRadar.Api` with `-PassThru` and records the PID;
`Stop-RunningApp` validates `^\d+$` and calls `Stop-Process -Id`. Secrets: `ANTHROPIC_API_KEY` injected
from vault entry `smithbros-claude-api-key`; `VOYAGE_API_KEY` to be added; `AppSettings.ToString()`
never renders them; an integration test asserts no key value appears in captured logs.

## R8 — Evaluation ground truth

**Decision**: For each investigation row of a registered vehicle with a non-empty `campaign_no`:
relevant set = { the investigation record } ∪ { recall records with that campaign number for the
vehicle }. Queries = complaints for the same vehicle whose `components` matches the investigation's
component prefix and whose `filed_on` lies within `[open_date, close_date]`, ordered by ODI number,
capped at 20 per investigation. Metrics: recall@5, recall@10, MRR per mode; faithfulness = verified ÷
emitted citations over a fixed 25-question set stored in `eval/questions.json`. Determinism: fixed
ordering, fixed caps, deterministic embeddings in tests; in production the embedding of a query is
cached so re-runs reuse it.

**Alternatives considered**: Hand-labelled relevance — rejected by FR-012. Using recall text as the
query — rejected, it leaks vocabulary into the answer; owner complaints are the realistic query.
