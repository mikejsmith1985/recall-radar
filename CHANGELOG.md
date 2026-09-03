# Changelog — Recall Radar

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Ingestion of one vehicle's NHTSA records with `ingest --vehicle "<display name>"`: complaints
  and recalls from the public APIs, defect investigations from the `FLAT_INV.zip` flat file,
  every record stored verbatim, cut into retrievable passages, and linked to the recall
  campaign it led to. Everything is fetched before anything is written, so a feed that fails
  part-way leaves the database as it was; repeat loads add nothing, because records are keyed
  by their NHTSA identifier and the F-150's body-style duplicates collapse on ODI number.
- The NHTSA client folder is read-only by construction and by test: only GET is used, and an
  integration test scans the folder and reflects over the clients to keep it that way.
- Recall dates are read month-first with a day-first fallback, because the recalls feed has
  returned both orders.
- Forge Workflow initialised: constitution, git hooks, Spec Kit pipeline carried over
  from the Randomness project so the same rules bind here from the first commit.
- Project scaffold for Recall Radar: .NET 10 solution with Domain, Retrieval, Ingest and
  Api projects, xUnit unit and integration test projects, a pgvector Postgres compose
  file, and a dev-run script that stops processes only by recorded PID (Article II).
- Domain retrieval: `ReciprocalRankFusion` (k = 60, deterministic tie-break by id) with per-list
  ranks kept so every hit can explain itself; `RetrievalMode`, `RankExplanation` and `RankedHit`.
- Domain evaluation: `GroundTruthCase` and `RetrievalMetrics` (recall@5, recall@10, MRR) that skip
  cases with no relevant documents instead of counting them as zero.
- Unit suite runs one test at a time and excuses exactly the first timed test of a run, so the
  10 ms Article V budget measures each test's own work rather than CPU contention or the
  runtime's one-off compilation of generic and assertion code.

### Changed
- The database connection string and the local Postgres password no longer live in source or
  compose. They come from a gitignored `.env` (template in `.env.example`) or the environment,
  and the app, `dotnet ef` and compose all fail with a pointed message when they are missing.
  Prompted by a GitGuardian finding on the scaffold PR: the value was a throwaway local
  password, but a password in a repository is a habit worth not having.

### Fixed
- The ingest command's settings file is named `ingest.settings.json`, not `appsettings.json`.
  Two referenced projects shipping the same filename put one file in a shared output folder,
  where the last build silently wins; the integration suite passed in one worktree and failed
  in another for exactly that reason.
- The ingest command reads its own `appsettings.json` no matter which directory it is run from.
  The host took its content root from the shell's working directory, so `dotnet run --project ...`
  from the repository root found no registered vehicles at all.
- Recalls for the F-150 are fetched under the base model name. NHTSA's two feeds do not share a
  model vocabulary: complaints are filed against a body style ("F-150 SUPER CREW") and the recalls
  feed answers that same string with 400 Bad Request, wanting "F-150". A registration may now carry
  a separate `RecallModel`, defaulting to the complaint model where one name serves both.
