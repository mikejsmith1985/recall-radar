# Changelog — Recall Radar

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Forge Workflow initialised: constitution, git hooks, Spec Kit pipeline carried over
  from the Randomness project so the same rules bind here from the first commit.
- Project scaffold for Recall Radar: .NET 10 solution with Domain, Retrieval, Ingest and
  Api projects, xUnit unit and integration test projects, a pgvector Postgres compose
  file, and a dev-run script that stops processes only by recorded PID (Article II).

### Changed
- The database connection string and the local Postgres password no longer live in source or
  compose. They come from a gitignored `.env` (template in `.env.example`) or the environment,
  and the app, `dotnet ef` and compose all fail with a pointed message when they are missing.
  Prompted by a GitGuardian finding on the scaffold PR: the value was a throwaway local
  password, but a password in a repository is a habit worth not having.
