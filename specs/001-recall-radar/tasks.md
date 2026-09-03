---

description: "Task list for Recall Radar"
---

# Tasks: Recall Radar

**Input**: Design documents from `/specs/001-recall-radar/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/http-api.md, contracts/cli.md, quickstart.md

**Tests**: REQUIRED. Constitution Article V — every implementation task is preceded by a failing test
(Red → Green → Refactor). The pre-commit hook rejects any new `.cs` file without a sibling
`<Name>Tests.cs` somewhere in the repo and any new `.tsx` without a `<Name>.test.tsx`, so every
source task below names its test file.

**Organization**: Phases map to feature branches. Each user story is independently testable.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 = grounded answer, US2 = explainable search, US3 = evaluation, US4 = load records
- Paths are repository-relative. `Unit` = `tests/RecallRadar.Unit/`, `Integration` = `tests/RecallRadar.Integration/`

## Path Conventions

- Backend: `src/RecallRadar.{Domain,Retrieval,Ingest,Api}/`
- Frontend: `web/src/`
- Tests: `tests/RecallRadar.Unit/`, `tests/RecallRadar.Integration/`, `tests/ux/`, `web/src/**/*.test.tsx`

---

## Phase 1: Setup — branch `feature/scaffold` (in progress)

**Purpose**: Solution, hooks, database, dev script. Most of this is already on disk.

- [x] T001 Create solution and six projects with references per plan.md (`RecallRadar.slnx`, `src/*`, `tests/*`)
- [x] T002 Add NuGet packages per plan.md; `dotnet-tools.json` with `dotnet-ef`
- [x] T003 [P] `Directory.Build.props` (nullable, warnings-as-errors, XML docs), `.editorconfig`, `.gitignore`
- [x] T004 [P] `docker-compose.yml` for `pgvector/pgvector:pg17` on host port 5433
- [x] T005 [P] Carry over `.forge/hooks/*`, `.specify/*`, constitution renamed; `CHANGELOG.md` with `[Unreleased]`
- [ ] T006 [P] Port `scripts/run-dev-clean.ps1` (PID file `.recall-radar.pid`, `-Stop`, `-CypressOnly`, `dotnet run --project src/RecallRadar.Api`, `Stop-Process -Id` only)
- [ ] T007 [P] Write `CLAUDE.md` for the new repo including the managed `<!-- SPECKIT START/END -->` block pointing at `specs/001-recall-radar/`
- [ ] T008 Unit-test speed budget: `tests/RecallRadar.Unit/UnitTestBudgetFixture.cs` + `UnitTestBudgetFixtureTests.cs` — a collection fixture that times each test with `Stopwatch` and fails the collection when any exceeds 10 ms
- [ ] T009 [P] Integration base: `tests/RecallRadar.Integration/PostgresContainerFixture.cs` + `PostgresContainerFixtureTests.cs` — starts `pgvector/pgvector:pg17` via Testcontainers, applies migrations, exposes a connection string
- [ ] T010 [P] Integration base: `tests/RecallRadar.Integration/Fixtures/RecordedHttpServer.cs` + `RecordedHttpServerTests.cs` — WireMock.Net server that maps `Fixtures/<service>/*.json` onto routes
- [ ] T011 Commit scaffold on `feature/scaffold`, push, open PR to `main`

---

## Phase 2: Foundational — branch `feature/persistence` (blocks all stories)

**Purpose**: Entities, DbContext, first migration, settings, health endpoint.

- [ ] T012 Test first: `Unit/Config/AppSettingsTests.cs` — `ToString()` never contains a configured key value; missing keys report `IsEmbeddingAvailable=false` / `IsAnsweringAvailable=false`
- [ ] T013 Implement `src/RecallRadar.Api/Config/AppSettings.cs` (`ConnectionString`, `AnthropicApiKey`, `VoyageApiKey`, `IsEmbeddingAvailable`, `IsAnsweringAvailable`, redacting `ToString`)
- [ ] T014 [P] Test first: `Unit/Persistence/RecallRadarDbContextTests.cs` — model builds; `SourceDocument` has unique (`VehicleId`,`Kind`,`ExternalId`); `DocumentChunk.Embedding` is `Vector` nullable
- [ ] T015 [P] Implement entities `src/RecallRadar.Retrieval/Persistence/Vehicle.cs`, `SourceDocument.cs`, `DocumentChunk.cs`, `InvestigationLink.cs`, `Answer.cs`, `EvaluationRun.cs` per data-model.md (each with a `<Name>Tests.cs` under `Unit/Persistence/` asserting required fields and invariants)
- [ ] T016 Implement `src/RecallRadar.Retrieval/Persistence/RecallRadarDbContext.cs` with `HasPostgresExtension("vector")`, generated `search_text` column, HNSW + GIN indexes, snake_case naming
- [ ] T017 Generate initial migration into `src/RecallRadar.Retrieval/Migrations/` (`dotnet ef migrations add InitialSchema --project src/RecallRadar.Retrieval --startup-project src/RecallRadar.Api`)
- [ ] T018 Integration test: `Integration/Persistence/MigrationTests.cs` — migrations apply on the container; `vector` extension present; inserting a duplicate (`VehicleId`,`Kind`,`ExternalId`) throws
- [ ] T019 Test first: `Integration/Endpoints/HealthEndpointTests.cs` via `WebApplicationFactory` — `/health` returns `database: ok`, `embeddings: unavailable`, `answering: unavailable` with no keys
- [ ] T020 Implement `src/RecallRadar.Api/Program.cs` + `src/RecallRadar.Api/Endpoints/HealthEndpoints.cs` (+ `Unit/Endpoints/HealthEndpointsTests.cs` for the status mapping) registering DbContext, `AppSettings`, problem details
- [ ] T021 Update `CHANGELOG.md`; commit; PR

**Checkpoint**: `dotnet ef database update` works against docker-compose; `/health` answers.

---

## Phase 3: User Story 4 — Load a vehicle's records (Priority: P4, but first in build order) — branch `feature/ingest`

**Goal**: Register a vehicle, fetch complaints/recalls/investigations read-only, store once, report counts.

**Independent Test**: Run `ingest --vehicle "2013 Explorer Sport"` against WireMock fixtures; counts match fixtures; second run reports `new 0`.

### Tests for User Story 4

- [ ] T022 [P] [US4] `Unit/Nhtsa/ComplaintsClientTests.cs` — parses fixture JSON to `NhtsaComplaint` records; `MM/DD/YYYY` dates; empty `results` → empty list
- [ ] T023 [P] [US4] `Unit/Nhtsa/RecallsClientTests.cs` — parses fixture; accepts both `MM/DD/YYYY` and `DD/MM/YYYY` for `ReportReceivedDate`, preferring month-first, and flags ambiguity
- [ ] T024 [P] [US4] `Unit/Nhtsa/ModelsClientTests.cs` — returns model strings for make/year
- [ ] T025 [P] [US4] `Unit/Nhtsa/InvestigationFlatFileParserTests.cs` — parses 11-column Latin-1 rows; filters by make/model/year; collapses rows sharing `action_no` per vehicle; keeps `campaign_no`
- [ ] T026 [P] [US4] `Unit/Records/PassageChunkerTests.cs` — complaint/recall → one chunk; investigation summary split at paragraphs ≤ 1,500 chars; ordinals sequential
- [ ] T027 [P] [US4] `Unit/Ingest/IngestPlannerTests.cs` — complaints from three model names with overlapping `odiNumber` collapse to one document per ODI; recall body is `Summary\n\nConsequence\n\nRemedy`
- [ ] T028 [US4] `Integration/Ingest/IngestCommandTests.cs` — end-to-end against WireMock (`Fixtures/nhtsa/complaints-explorer-2013.json`, `recalls-explorer-2013.json`, `models-ford-2013.json`, `FLAT_INV-sample.zip`) and the container: counts stored; re-run idempotent; a 500 from WireMock mid-way leaves zero rows (transaction)
- [ ] T029 [US4] `Integration/Ingest/NoMutatingVerbsTests.cs` — reflection/grep test asserting no `POST`/`PUT`/`DELETE`/`PATCH` HttpMethod is used anywhere under `src/RecallRadar.Ingest/Nhtsa/`

### Implementation for User Story 4

- [ ] T030 [P] [US4] `src/RecallRadar.Ingest/Nhtsa/NhtsaComplaint.cs`, `NhtsaRecall.cs`, `NhtsaInvestigationRow.cs` DTOs (tests: T022/T023/T025 cover them; add `NhtsaComplaintTests.cs` etc. as thin field tests to satisfy the gate)
- [ ] T031 [P] [US4] `src/RecallRadar.Ingest/Nhtsa/ComplaintsClient.cs`, `RecallsClient.cs`, `ModelsClient.cs` — GET only, typed `HttpClient` with `AddStandardResilienceHandler()`
- [ ] T032 [P] [US4] `src/RecallRadar.Ingest/Nhtsa/InvestigationFlatFileParser.cs` — downloads `FLAT_INV.zip` (GET), streams `FLAT_INV.txt` Latin-1
- [ ] T033 [P] [US4] `src/RecallRadar.Domain/Records/RecordKind.cs`, `RetrievablePassage.cs`, `PassageChunker.cs` (tests T026 + `RecordKindTests.cs`, `RetrievablePassageTests.cs`)
- [ ] T034 [US4] `src/RecallRadar.Ingest/IngestPlanner.cs` — pure mapping from NHTSA DTOs to `SourceDocument` + chunks + links, dedupe by ODI
- [ ] T035 [US4] `src/RecallRadar.Ingest/Commands/VehiclesCommand.cs` (+ `Unit/Commands/VehiclesCommandTests.cs`) — `vehicles add|list`, validates model names via `ModelsClient`
- [ ] T036 [US4] `src/RecallRadar.Ingest/Commands/IngestCommand.cs` (+ `Unit/Commands/IngestCommandTests.cs` for output formatting) — one transaction per vehicle, upsert by unique key, prints the contract output, `--skip-embed`
- [ ] T037 [US4] `src/RecallRadar.Ingest/Program.cs` (+ `Unit/Commands/ProgramTests.cs` asserting verb registration) — `System.CommandLine` root with `vehicles`, `ingest`, `embed`, `eval`
- [ ] T038 [US4] `src/RecallRadar.Api/Endpoints/VehicleEndpoints.cs` (+ `Integration/Endpoints/VehicleEndpointsTests.cs`) — `GET/POST /api/vehicles` per http-api.md
- [ ] T039 [US4] Run quickstart §2 against live NHTSA once; record real counts in `CHANGELOG.md`; commit; PR

**Checkpoint**: Two vehicles loaded locally; `/api/vehicles` shows counts.

---

## Phase 4: User Story 2 — See why each record was retrieved (Priority: P2) — branch `feature/hybrid-search`

**Goal**: dense / sparse / hybrid search with per-method ranks; sparse works with no key.

**Independent Test**: Seed 30 chunks with deterministic embeddings; `GET /api/search` in each mode returns different orderings and every hit carries `denseRank`, `sparseRank`, `fusedScore`.

### Tests for User Story 2

- [ ] T040 [P] [US2] `Unit/Retrieval/ReciprocalRankFusionTests.cs` — k=60; item first in both lists scores 2/61; item in one list only scores 1/(60+rank); ties broken by lower id; empty inputs → empty
- [ ] T041 [P] [US2] `Unit/Retrieval/RankedHitTests.cs` — nullable ranks; `FusedScore` rounding
- [ ] T042 [P] [US2] `Unit/Embeddings/DeterministicEmbeddingGeneratorTests.cs` — same text → same 1024-d unit vector; different text → different vector
- [ ] T043 [P] [US2] `Unit/Embeddings/VoyageEmbeddingGeneratorTests.cs` — builds the request body (`model: voyage-3.5`, `input: [...]`, batches ≤ 128), parses the response, throws a typed error on 401, never logs the key (uses a stub `HttpMessageHandler`)
- [ ] T044 [P] [US2] `Unit/Search/SearchRequestTests.cs` — validation: `q` 2–500 chars, `limit` ≤ 50, unknown mode rejected
- [ ] T045 [US2] `Integration/Search/HybridSearchServiceTests.cs` — seeded container: sparse finds keyword matches ranked by `ts_rank_cd`; dense orders by cosine; hybrid = RRF of both; `component` and date filters restrict; other vehicle's chunks never appear; `dense` on a vehicle with null embeddings throws `EmbeddingsUnavailableException`
- [ ] T046 [US2] `Integration/Endpoints/SearchEndpointTests.cs` — `GET /api/search` per http-api.md; 409 for dense/hybrid without key; 404 unknown vehicle

### Implementation for User Story 2

- [ ] T047 [P] [US2] `src/RecallRadar.Domain/Retrieval/RetrievalMode.cs`, `RankedHit.cs`, `ReciprocalRankFusion.cs` (+ `RetrievalModeTests.cs`)
- [ ] T048 [P] [US2] `src/RecallRadar.Retrieval/Embeddings/DeterministicEmbeddingGenerator.cs`
- [ ] T049 [P] [US2] `src/RecallRadar.Retrieval/Embeddings/VoyageEmbeddingGenerator.cs` — implements `IEmbeddingGenerator<string, Embedding<float>>`; drift-justification comment (Article VII: no Voyage C# SDK)
- [ ] T050 [P] [US2] `src/RecallRadar.Retrieval/Embeddings/EmbeddingsUnavailableException.cs` (+ `EmbeddingsUnavailableExceptionTests.cs`)
- [ ] T051 [US2] `src/RecallRadar.Retrieval/Search/SearchRequest.cs`, `SearchHit.cs` (+ `SearchHitTests.cs`)
- [ ] T052 [US2] `src/RecallRadar.Retrieval/Search/HybridSearchService.cs` — the three SQL paths from research R4 via `FromSqlInterpolated`; fusion via Domain; returns ranks per method; drift-justification comment (Article VII: VectorData hides per-method ranks)
- [ ] T053 [US2] `src/RecallRadar.Ingest/Commands/EmbedCommand.cs` (+ `Unit/Commands/EmbedCommandTests.cs`) — back-fills null embeddings in batches ≤ 128; exit 1 with `error: VOYAGE_API_KEY not set`
- [ ] T054 [US2] `src/RecallRadar.Api/Endpoints/SearchEndpoints.cs` — maps mode/filters, translates `EmbeddingsUnavailableException` → 409 problem
- [ ] T055 [US2] Wire `IEmbeddingGenerator` registration in `src/RecallRadar.Api/Program.cs` and `src/RecallRadar.Ingest/Program.cs`: Voyage when key present, otherwise a `NullEmbeddingGenerator` that throws `EmbeddingsUnavailableException` (+ `NullEmbeddingGeneratorTests.cs`)
- [ ] T056 [US2] `CHANGELOG.md`; commit; PR

**Checkpoint**: quickstart §3 passes (sparse); §4 ready to pass once the Voyage key exists.

---

## Phase 5: User Story 1 — Ask whether a symptom is a known problem (Priority: P1) 🎯 MVP — branch `feature/grounded-answer`

**Goal**: `/api/ask` returns a structured answer whose every citation is verified verbatim; failures counted; zero survivors → not grounded.

**Independent Test**: With WireMock returning a recorded Anthropic response containing one true quote and one fabricated quote, `/api/ask` returns one citation, `droppedCitationCount: 1`; with all fabricated, `isGrounded: false`.

### Tests for User Story 1

- [ ] T057 [P] [US1] `Unit/Grounding/QuoteVerifierTests.cs` — exact match passes; differing line wrapping passes; differing case fails; paraphrase fails; `documentId` not in the supplied set fails; empty quote fails; offsets returned against the original body; order preserved
- [ ] T058 [P] [US1] `Unit/Grounding/GroundedAnswerTests.cs` — `IsGrounded` is false iff no verified citations; dropped count = emitted − verified
- [ ] T059 [P] [US1] `Unit/Answering/AnswerPromptTests.cs` — prompt contains every retrieved document id and body; system prompt states quotes are checked character-for-character; stable ordering by document id (cache-friendly)
- [ ] T060 [P] [US1] `Unit/Answering/AnswerResponseParserTests.cs` — parses the JSON schema; missing field → parse failure result, not exception; `stop_reason: refusal` → not-grounded result with reason
- [ ] T061 [US1] `Integration/Answering/AnswerServiceTests.cs` — WireMock Anthropic fixtures `Fixtures/anthropic/ask-exhaust-one-fabricated.json`, `ask-all-fabricated.json`, `ask-refusal.json`; asserts persisted `answers` rows and `droppedCitationCount`
- [ ] T062 [US1] `Integration/Endpoints/AskEndpointTests.cs` — `POST /api/ask` per http-api.md; 503 without key; never 500 on model failure
- [ ] T063 [P] [US1] `Integration/Endpoints/DocumentEndpointTests.cs` — `GET /api/documents/{id}` returns verbatim body
- [ ] T064 [P] [US1] `Integration/Config/NoCredentialLeakTests.cs` — run an ask through the app with a fake key value and assert the value appears in no captured log line or response body

### Implementation for User Story 1

- [ ] T065 [P] [US1] `src/RecallRadar.Domain/Grounding/Citation.cs`, `VerifiedCitation.cs`, `GroundedAnswer.cs`, `QuoteVerifier.cs` (+ `CitationTests.cs`, `VerifiedCitationTests.cs`) — port of Randomness `_appears_verbatim`
- [ ] T066 [P] [US1] `src/RecallRadar.Api/Answering/AnswerPrompt.cs` — system prompt + user prompt builder; `AnswerSchema.cs` (+ `AnswerSchemaTests.cs`) — the JSON schema from research R5
- [ ] T067 [US1] `src/RecallRadar.Api/Answering/AnswerResponseParser.cs`
- [ ] T068 [US1] `src/RecallRadar.Api/Answering/AnswerService.cs` — retrieve (hybrid, or sparse when embeddings unavailable) → `client.Messages.Create` with `Model = "claude-opus-5"`, `OutputConfig.Format` JSON schema, `MaxTokens = 4096` → parse → verify → persist; refusal handled; server-side fallbacks intentionally off (R5)
- [ ] T069 [US1] `src/RecallRadar.Api/Endpoints/AskEndpoints.cs`, `DocumentEndpoints.cs` — 503 without key; problem details for failures
- [ ] T070 [US1] Register `AnthropicClient` in `Program.cs` only when `IsAnsweringAvailable`
- [ ] T071 [US1] Run quickstart §5 once with the vault-injected key against loaded data; note the real `droppedCitationCount` in `CHANGELOG.md`; commit; PR

**Checkpoint**: MVP — a grounded, verified answer for the 2013 Explorer exhaust question.

---

## Phase 6: User Story 3 — Measure retrieval quality (Priority: P3) — branch `feature/evaluation`

**Goal**: Deterministic recall@5 / recall@10 / MRR per mode from derived ground truth, plus faithfulness.

**Independent Test**: Seeded container with one investigation linked to one campaign and six complaints in its window; `eval` reports 6 cases and identical numbers on re-run.

### Tests for User Story 3

- [ ] T072 [P] [US3] `Unit/Evaluation/RetrievalMetricsTests.cs` — recall@k and MRR on hand-computed tiny cases; empty relevant set excluded; deterministic
- [ ] T073 [P] [US3] `Unit/Evaluation/GroundTruthCaseTests.cs` — value semantics
- [ ] T074 [US3] `Integration/Evaluation/GroundTruthBuilderTests.cs` — builds cases from `investigation_links`: relevant = investigation + campaign recalls; queries = same-vehicle complaints with matching component prefix inside the open/close window, ordered by ODI, capped at 20; investigations without campaign excluded
- [ ] T075 [US3] `Integration/Evaluation/EvaluationRunnerTests.cs` — runs all modes with deterministic embeddings; stores `evaluation_runs`; two runs → identical `metrics_json`; modes needing embeddings skipped with note when unavailable
- [ ] T076 [US3] `Integration/Endpoints/EvalEndpointTests.cs` — `GET /api/eval` returns `latest` + `history`; `latest: null` before any run

### Implementation for User Story 3

- [ ] T077 [P] [US3] `src/RecallRadar.Domain/Evaluation/GroundTruthCase.cs`, `RetrievalMetrics.cs`
- [ ] T078 [US3] `src/RecallRadar.Retrieval/Evaluation/GroundTruthBuilder.cs`
- [ ] T079 [US3] `src/RecallRadar.Retrieval/Evaluation/EvaluationRunner.cs` — per-mode runs via `HybridSearchService`; faithfulness over `eval/questions.json` (25 fixed questions, committed) through `AnswerService` when answering is available, else reported as skipped
- [ ] T080 [US3] `src/RecallRadar.Ingest/Commands/EvalCommand.cs` (+ `Unit/Commands/EvalCommandTests.cs` for the table formatting) — prints the cli.md table; writes `eval/results.json`
- [ ] T081 [US3] `src/RecallRadar.Api/Endpoints/EvalEndpoints.cs`
- [ ] T082 [US3] Run quickstart §6 on real data; commit `eval/results.json` and the numbers into `README.md` + `CHANGELOG.md`; PR

**Checkpoint**: Numbers exist and are reproducible.

---

## Phase 7: Web client + Cypress — branch `feature/web-client` (serves US1, US2, US3 UI)

**Goal**: Vehicle picker, search with rank explanation and mode switch, ask with highlighted citations, eval table; real-event Cypress suite that cannot pass against an empty database.

**Independent Test**: `run-dev-clean.ps1 -CypressOnly` seeds a fixture DB and passes; the same suite fails immediately against an unseeded DB.

### Tests

- [ ] T083 [P] `web/src/api/client.test.ts` — typed client builds correct URLs/bodies for every endpoint in http-api.md
- [ ] T084 [P] `web/src/components/VehiclePicker.test.tsx`, `SearchBox.test.tsx`, `ResultCard.test.tsx` (shows dense/sparse/fused), `CitationView.test.tsx` (highlights `[start,end)`), `EvalTable.test.tsx`, `ModeSwitch.test.tsx` — Vitest + Testing Library
- [ ] T085 [P] `web/src/pages/SearchPage.test.tsx`, `AskPage.test.tsx`, `EvalPage.test.tsx` — render with a mocked client
- [ ] T086 `tests/ux/support/e2e.ts` — imports `cypress-real-events`; `before()` calls `/api/vehicles` and fails unless at least one vehicle has `counts.complaint ≥ 1`
- [ ] T087 `tests/ux/e2e/search.cy.ts` — `realType` a query, `realClick` each mode, assert ordering changes and rank badges present
- [ ] T088 `tests/ux/e2e/ask.cy.ts` — ask against the seeded fixture (API answering served by the seeded `answers` row or a WireMock-backed dev profile), assert citation highlight opens the record
- [ ] T089 `tests/ux/e2e/eval.cy.ts` — eval table renders three modes and faithfulness

### Implementation

- [ ] T090 [P] Scaffold `web/` with Vite React TS; `vite.config.ts` proxying `/api` to `http://127.0.0.1:5180`; Vitest + Testing Library configured in `web/package.json`
- [ ] T091 [P] `web/src/api/client.ts` — typed fetch client
- [ ] T092 [P] Components in `web/src/components/` (`VehiclePicker.tsx`, `SearchBox.tsx`, `ModeSwitch.tsx`, `ResultCard.tsx`, `CitationView.tsx`, `EvalTable.tsx`)
- [ ] T093 Pages in `web/src/pages/` (`SearchPage.tsx`, `AskPage.tsx`, `EvalPage.tsx`) and `web/src/App.tsx` + `main.tsx` (+ `App.test.tsx`, `main.test.tsx` smoke tests to satisfy the gate)
- [ ] T094 Serve built SPA from `src/RecallRadar.Api/Program.cs` (`UseStaticFiles` + SPA fallback) — covered by `Integration/Endpoints/SpaFallbackTests.cs`
- [ ] T095 `tests/ux/cypress.config.ts` (`baseUrl: http://127.0.0.1:5180`), `tests/ux/package.json` (cypress ^13.17, cypress-real-events ^1.14)
- [ ] T096 Fixture seeding for UX: `src/RecallRadar.Ingest/Commands/SeedFixtureCommand.cs` (+ `Unit/Commands/SeedFixtureCommandTests.cs`) loads `tests/ux/fixtures/*.json` into a throwaway database named by `--database`; `run-dev-clean.ps1 -CypressOnly` calls it, starts the API with that connection string, runs Cypress, stops by PID
- [ ] T097 `CHANGELOG.md`; commit; PR

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T098 [P] `README.md` — what it is, the eval numbers with a link to `eval/results.json` and the command that produced them, screenshots of the rank explanation
- [ ] T099 [P] Vault documentation in `CLAUDE.md`: secret names, injection command, what runs without which key
- [ ] T100 Full-history secret scan (`git log -p | grep` for key patterns) before the repo is made public
- [ ] T101 Run quickstart.md top to bottom on a clean clone; fix anything that drifted
- [ ] T102 Delete merged branches

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** → **Foundational (Phase 2)** → everything else.
- **US4 ingest (Phase 3)** must precede **US2 search (Phase 4)** in practice (search needs rows), but
  Phase 4's integration tests seed their own rows, so the two branches can be developed in parallel
  and merged in order.
- **US1 answer (Phase 5)** depends on Phase 4's `HybridSearchService` (sparse path suffices).
- **US3 eval (Phase 6)** depends on Phases 4 and 5.
- **Web (Phase 7)** depends on the API contracts only; can start after Phase 2 against mocked
  responses, and its Cypress suite needs Phases 3–6 merged.

### Within Each Phase

- Tests first, confirmed failing, then implementation.
- Domain types → Retrieval services → Api endpoints → CLI verbs.
- `CHANGELOG.md` updated in the same PR.

### Parallel Opportunities

- Phase 3: T022–T027 in parallel; T030–T033 in parallel.
- Phase 4: T040–T044 in parallel; T047–T050 in parallel.
- Phase 5: T057–T060, T063–T064 in parallel; T065–T066 in parallel.
- Phase 7 can be handed to a separate agent from Phase 4 onward.

---

## Parallel Example: User Story 2

```bash
Task: "Unit/Retrieval/ReciprocalRankFusionTests.cs"
Task: "Unit/Embeddings/DeterministicEmbeddingGeneratorTests.cs"
Task: "Unit/Embeddings/VoyageEmbeddingGeneratorTests.cs"
Task: "Unit/Search/SearchRequestTests.cs"
# then
Task: "src/RecallRadar.Domain/Retrieval/ReciprocalRankFusion.cs"
Task: "src/RecallRadar.Retrieval/Embeddings/DeterministicEmbeddingGenerator.cs"
Task: "src/RecallRadar.Retrieval/Embeddings/VoyageEmbeddingGenerator.cs"
```

---

## Implementation Strategy

### MVP First

1. Phases 1–2 (scaffold, persistence).
2. Phase 3 (ingest) — real data on disk.
3. Phase 4 sparse path only — search works with no key.
4. Phase 5 — the grounded answer. **This is the demo.**
5. Validate quickstart §1–3, §5.

### Incremental Delivery

- Add the Voyage key → run `embed` → dense/hybrid light up with no code change.
- Phase 6 turns the demo into a measured claim.
- Phase 7 makes it visible.

### Notes

- Every new `.cs` needs a `<Name>Tests.cs` and every `.tsx` a `.test.tsx` or the commit is blocked.
- `src/RecallRadar.Retrieval/Migrations/` is exempt from that gate (hook regex `/[Mm]igrations/`).
- No task may add a mutating HTTP verb under `src/RecallRadar.Ingest/Nhtsa/` — T029 enforces it.
