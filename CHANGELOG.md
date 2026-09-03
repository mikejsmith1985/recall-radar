# Changelog — Recall Radar

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Domain grounding: `QuoteVerifier` checks a model's quote against the stored record body with
  only whitespace normalised (an exact port of the Randomness `_appears_verbatim` rule), returning
  offsets into the original text so the UI can highlight the passage; `CitationCheck` runs every
  citation, drops the ones that fail with a stated reason, and marks an answer grounded only when at
  least one survives; `GroundedAnswer` downgrades a known-pattern claim that has no surviving evidence.
- Domain retrieval: `ReciprocalRankFusion` (k = 60, deterministic tie-break by id) with per-list
  ranks kept so every hit can explain itself; `RetrievalMode`, `RankExplanation` and `RankedHit`.
- Domain evaluation: `GroundTruthCase` and `RetrievalMetrics` (recall@5, recall@10, MRR) that skip
  cases with no relevant documents instead of counting them as zero.
- Unit suite runs one test at a time and excuses exactly the first timed test of a run, so the
  10 ms Article V budget measures each test's own work rather than CPU contention or the
  runtime's one-off compilation of generic and assertion code.
- Forge Workflow initialised: constitution, git hooks, Spec Kit pipeline carried over
  from the Randomness project so the same rules bind here from the first commit.
- Project scaffold for Recall Radar: .NET 10 solution with Domain, Retrieval, Ingest and
  Api projects, xUnit unit and integration test projects, a pgvector Postgres compose
  file, and a dev-run script that stops processes only by recorded PID (Article II).
