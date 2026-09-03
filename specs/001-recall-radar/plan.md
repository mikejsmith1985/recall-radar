# Implementation Plan: Recall Radar

**Branch**: `001-recall-radar` | **Date**: 2026-09-03 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-recall-radar/spec.md`

## Summary

Recall Radar answers "my vehicle does X — is that a known problem?" by retrieving NHTSA complaints,
recalls and defect investigations for one registered vehicle, ranking them with a hand-written hybrid
query (pgvector cosine + Postgres full-text, fused with Reciprocal Rank Fusion), and asking Claude for a
structured answer whose every citation is then checked as a literal whitespace-normalised substring of
the stored record. Citations that fail are dropped and counted; an answer with none left is labelled
"not grounded". Retrieval quality is measured against ground truth derived from NHTSA's own
investigation → recall-campaign links, per retrieval mode, and reported on an evaluation page.

## Technical Context

**Language/Version**: C# on .NET 10 (SDK 10.0.400); TypeScript 5 for the web client

**Primary Dependencies**: ASP.NET Core minimal API; `Anthropic` 12.x (official C# SDK, `claude-opus-5`,
structured output via `OutputConfig.Format`); `Microsoft.Extensions.AI.Abstractions` 10.x
(`IEmbeddingGenerator<string, Embedding<float>>`); `Npgsql.EntityFrameworkCore.PostgreSQL` 10.x +
`Pgvector.EntityFrameworkCore` 0.3.x; `Microsoft.Extensions.Http.Resilience` (standard retry pipeline);
`System.CommandLine` for the ingest/eval CLI; React 19 + Vite for the web client

**Storage**: PostgreSQL 17 with the `vector` extension (`pgvector/pgvector:pg17`), EF Core migrations;
HNSW index (cosine) on embeddings, GIN index on a generated `tsvector` column

**Testing**: xUnit + NSubstitute (unit, 10 ms per-test budget enforced by a shared fixture);
`Testcontainers.PostgreSql` + `WireMock.Net` (integration, real Postgres + HTTP doubles for NHTSA,
Voyage and Anthropic); Cypress 13 + `cypress-real-events` (UX) launched only through
`scripts/run-dev-clean.ps1`; Vitest for React component unit tests

**Target Platform**: Windows 11 developer machine; Docker Desktop for Postgres; browser client

**Project Type**: Web application — .NET API + CLI backend, React frontend, single repository

**Performance Goals**: Search results under 2 s for a loaded vehicle (SC-005); one-vehicle ingest under
10 minutes (SC-004); evaluation run deterministic and repeatable (SC-006)

**Constraints**: Read-only against NHTSA (no mutating verbs in `src/RecallRadar.Ingest/Nhtsa/`);
zero unverified citations displayed (SC-001); secrets only via Forge Vault; process control by PID file
only; unit tests never touch network, disk or database

**Scale/Scope**: Two vehicles initially (~3,600 complaints, 20 recalls, ~30 investigation rows);
design holds for tens of vehicles without change; single local user

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Article | Gate | Status |
|---|---|---|
| I Prime Directive | Best route; parallelisable work identified (phases map to independent branches; UI and ingest can proceed concurrently) | PASS |
| II Process Protection | `run-dev-clean.ps1` stops only by recorded PID; no name-pattern kills anywhere | PASS — script ported from Randomness with a renamed PID file |
| III Branching | Feature branches per phase, PR to `main` | PASS |
| IV Code Quality | Self-documenting names, `is/has/can/should/was` booleans, verb-first methods, files open with a purpose comment, public members XML-documented, functions ≤ 40 lines, `TreatWarningsAsErrors` | PASS — enforced by `Directory.Build.props` and hooks |
| V Testing | Unit fully mocked and < 10 ms; integration on Testcontainers; UX via Cypress real events through `run-dev-clean.ps1`; Red → Green → Refactor | PASS — see research R6 |
| VI Documentation | `CHANGELOG.md` per PR; no status documents outside `specs/` | PASS |
| VII Framework-First | Every custom component carries a drift justification (research R2, R4, R5) | PASS |
| VIII Release | Local pipeline only; no GitHub Actions release | PASS — release is out of scope for this feature |
| IX Vault Zero-Knowledge | `ANTHROPIC_API_KEY` from the Anthropic key entry in the Forge Vault; `VOYAGE_API_KEY` pending; settings type refuses to render secrets; no-leak test | PASS |
| X Verification & Proof | Quote verifier, eval harness with derived ground truth, quickstart scenarios with expected numbers | PASS |
| XI Output Restraint | No dashboards beyond the app itself | PASS |

**Post-design re-check (after Phase 1)**: no new violations. The four-project split (Domain /
Retrieval / Ingest / Api) is justified in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/001-recall-radar/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (http-api.md, cli.md)
├── checklists/          # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
RecallRadar.slnx
Directory.Build.props                 # nullable, warnings-as-errors, XML docs
docker-compose.yml                    # pgvector/pgvector:pg17 for local dev only
dotnet-tools.json                     # dotnet-ef local tool

src/
├── RecallRadar.Domain/               # pure, I/O-free, no package references
│   ├── Vehicles/                     # VehicleIdentity
│   ├── Records/                      # RecordKind, RetrievablePassage, PassageChunker
│   ├── Retrieval/                    # RetrievalMode, RankedHit, ReciprocalRankFusion
│   ├── Grounding/                    # Citation, VerifiedCitation, GroundedAnswer, QuoteVerifier
│   └── Evaluation/                   # GroundTruthCase, RetrievalMetrics (recall@k, MRR)
├── RecallRadar.Retrieval/            # EF Core + search + eval over the database
│   ├── Persistence/                  # RecallRadarDbContext, entities, configurations
│   ├── Migrations/                   # EF Core migrations (excluded from the test-file gate)
│   ├── Search/                       # HybridSearchService (dense / sparse / hybrid SQL)
│   ├── Embeddings/                   # VoyageEmbeddingGenerator, DeterministicEmbeddingGenerator
│   └── Evaluation/                   # GroundTruthBuilder, EvaluationRunner
├── RecallRadar.Ingest/               # console: `vehicles`, `ingest`, `embed`, `eval` verbs
│   ├── Nhtsa/                        # READ-ONLY clients: complaints, recalls, models, FLAT_INV parser
│   └── Commands/                     # System.CommandLine verbs
└── RecallRadar.Api/                  # minimal API + serves built SPA
    ├── Endpoints/                    # /health, /api/vehicles, /api/search, /api/ask, /api/documents, /api/eval
    ├── Answering/                    # AnswerService (Anthropic structured output), AnswerPrompt
    └── Config/                       # AppSettings (secret-redacting)

web/                                  # Vite + React + TypeScript client
├── src/
│   ├── components/                   # VehiclePicker, SearchBox, ResultCard, CitationView, EvalTable
│   ├── pages/                        # SearchPage, AskPage, EvalPage
│   └── api/                          # typed fetch client against contracts/http-api.md
└── vite.config.ts

tests/
├── RecallRadar.Unit/                 # xUnit + NSubstitute; UnitTestBudgetFixture enforces 10 ms
├── RecallRadar.Integration/          # Testcontainers.PostgreSql + WireMock.Net; recorded fixtures
│   └── Fixtures/                     # nhtsa/*.json, voyage/*.json, anthropic/*.json
└── ux/                               # Cypress: cypress.config.ts, e2e/*.cy.ts, support/e2e.ts

scripts/
└── run-dev-clean.ps1                 # -Stop, -CypressOnly; PID file `.recall-radar.pid`
```

**Structure Decision**: Web application layout with the backend split into four .NET projects and the
frontend under `web/`. `Domain` holds every pure algorithm (fusion, verification, metrics, chunking) so
it is unit-testable inside the 10 ms budget with no doubles at all; `Retrieval` owns the database and
the SQL; `Ingest` is the only project allowed to talk to NHTSA; `Api` is the only project allowed to
talk to Anthropic. Test projects mirror Article V's three layers.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Four source projects instead of one | Compile-time enforcement of the read-only and secrets boundaries: only `Ingest` references the NHTSA clients, only `Api` references the Anthropic SDK, `Domain` has no package references at all | A single project would rely on discipline and a grep to keep NHTSA writes and secret handling out of the wrong place; project references make the boundary a build error |
| Hand-written hybrid SQL instead of `Microsoft.Extensions.VectorData` | The retrieval query *is* the learning payload, and the rank-explanation feature (FR-007) needs per-method ranks the abstraction does not expose | The vector-store abstraction hides the fusion step and returns one score per hit, which cannot show "found by meaning vs keyword" |
| Custom `VoyageEmbeddingGenerator` | Voyage AI ships no C# SDK and no `Microsoft.Extensions.AI` provider package | Using a provider that does ship one (OpenAI, Ollama) changes the embedding vendor, not the amount of custom code; the adapter is ~60 lines behind the framework interface |
