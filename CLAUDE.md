# Recall Radar — Forge Agent Instructions

> Claude Code reads this file automatically at every session start. All workflow
> rules below are binding — read them before beginning any task.

@.specify/memory/constitution.md

## Forge Awareness — Enforced Mode (binding, every task)

You are running inside Forge Terminal.

- **Protected processes**: `fterm.exe` / `forge.exe`. Never kill by name pattern — target a specific PID only (Article II).
- **Before starting any task**, read every rule in `.specify/memory/constitution.md`.
- **Apply the `workflow-enforcer` rules to every task**, without exception.
- **Mandatory skill invocation order**: `workflow-enforcer` → `code-quality` → `framework-first` → `branching-strategy`.
- **No shortcuts**: quality gates, naming rules, and TDD apply on every change.

## What this is

Recall Radar answers one question for a specific vehicle: *"My truck does X — is that a known
defect, and is there a recall or investigation for it?"* It retrieves from public NHTSA
complaints, recalls and defect investigations using hybrid search (pgvector + Postgres full-text,
fused with Reciprocal Rank Fusion), then asks Claude for an answer whose every citation is
checked as a literal quote from the source record. A retrieval evaluation harness scores
dense, sparse and hybrid modes against ground truth derived from investigation → recall links.

<!-- SPECKIT START -->

## Active Feature

**Recall Radar** — `specs/001-recall-radar/`

- Spec: `specs/001-recall-radar/spec.md`
- Plan: `specs/001-recall-radar/plan.md`
- Research: `specs/001-recall-radar/research.md`
- Data model: `specs/001-recall-radar/data-model.md`
- Contracts: `specs/001-recall-radar/contracts/`
- Quickstart: `specs/001-recall-radar/quickstart.md`
- Tasks: `specs/001-recall-radar/tasks.md`

<!-- SPECKIT END -->

## Stack

.NET 10 (C#) · ASP.NET Core minimal API · EF Core 10 + Npgsql + pgvector · Microsoft.Extensions.AI
(`IEmbeddingGenerator`) · Anthropic C# SDK (`claude-opus-5`) · React + TypeScript (Vite) ·
xUnit + NSubstitute / Testcontainers.PostgreSql + WireMock.Net / Cypress + cypress-real-events

| Command | Purpose |
|---|---|
| `Copy-Item .env.example .env` then edit | one-time local setup; `.env` is gitignored and holds the DB password and connection string |
| `docker compose up -d` | local pgvector Postgres on port 5433 (password from `.env`) |
| `dotnet build` / `dotnet test tests/RecallRadar.Unit` | build; unit layer (mocked, 10 ms budget) |
| `dotnet test tests/RecallRadar.Integration` | integration layer (real containers) |
| `dotnet ef migrations add <Name> -p src/RecallRadar.Retrieval -s src/RecallRadar.Ingest` | schema change |
| `scripts/run-dev-clean.ps1` / `-Stop` / `-CypressOnly` | run the app by PID file; UX layer |

## Non-negotiables

- The application is **read-only against NHTSA**. No mutating HTTP verb may exist in `src/RecallRadar.Ingest/Nhtsa/`.
- A model-produced quote is not evidence until verified as a literal (whitespace-normalised) substring of its source record's stored body. Unverified citations are dropped and counted, never silently passed.
- An answer with zero surviving citations is returned as **not grounded**. Grounding fails closed.
- Retrieval explanations (dense rank, sparse rank, fused score) are part of the API contract, not a debug extra.
- Secrets come only from the Forge Vault (`ANTHROPIC_API_KEY`, `VOYAGE_API_KEY`) or the gitignored `.env` (`RECALLRADAR_CONNECTION`, `RECALLRADAR_DB_PASSWORD`). No connection string or password is ever written into source, including local defaults. Settings never render secret values.
- `scripts/run-dev-clean.ps1` stops processes **by PID from a PID file only** — never a name pattern (Article II).
- Unit tests never touch the network, disk, or a database. Integration tests never touch live NHTSA, Voyage, or Anthropic.
