# Changelog — Recall Radar

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Web client (`web/`, Vite + React + TypeScript): vehicle picker, explainable search with a
  ranking-mode switch that disables meaning-based modes and says why when no embedding key is
  configured, result cards that show each record's meaning rank, keyword rank and fused score,
  a grounded-answer panel that labels answers grounded or not grounded and always shows the
  dropped-citation count, a citation view that highlights the verified span inside the verbatim
  record, a related recalls and investigations panel, and an evaluation table. Vitest covers
  every component and page against a mocked client; the dev server proxies `/api` and `/health`
  to the .NET API on port 5180.
- Cypress UX suite (`tests/ux/`) driven only by real pointer and keyboard events, with a support
  hook that refuses to run unless at least one vehicle has loaded complaints, so a green run
  cannot come from an empty database.
- Forge Workflow initialised: constitution, git hooks, Spec Kit pipeline carried over
  from the Randomness project so the same rules bind here from the first commit.
- Project scaffold for Recall Radar: .NET 10 solution with Domain, Retrieval, Ingest and
  Api projects, xUnit unit and integration test projects, a pgvector Postgres compose
  file, and a dev-run script that stops processes only by recorded PID (Article II).
