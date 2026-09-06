# Changelog — Recall Radar

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- The add-vehicle form offers NHTSA's own model names for the make and year entered, through a new
  `GET /api/nhtsa/models`. Their vocabulary is not the one on the car — a Mach-E is filed as
  `MUSTANG MACH-E BEV BEV` — and Ford alone has 57 names for one model year. Losing the list does
  not block anything: the name can still be typed.

### Changed
- The browser suite runs in Chrome rather than Cypress's bundled Electron. Electron 118 crashes with
  an access violation on real keystrokes into an input bound to a `<datalist>`; Chrome handles the
  same steps, and it is the browser people actually use.
- One `createStubClient` builds the test double for the API client, where every method refuses
  loudly and a test names only the calls it expects. Adding a client method had come to mean editing
  seven test files.

### Fixed
- A rejected model name now suggests the closest names rather than listing the first twenty
  alphabetically. The old message stopped at `F-59` and hid `MUSTANG MACH-E BEV BEV`, which was the
  name being reached for, while looking like a complete list. Suggestions are ranked by how many
  characters of the typed name they account for, so a one-letter match cannot outrank a whole word,
  and the count of names not shown is stated.

- Vehicles can be added in the app. `POST /api/vehicles` validates the model name against NHTSA's
  own list, then queues the load and answers 202 with somewhere to watch it: the work reaches three
  feeds and takes minutes, so no request could wait for it. Asking twice joins the load already
  running rather than starting a second pass over feeds that belong to somebody else.
- A background runner takes one load at a time and records the outcome on the job either way. A
  failure is stored with its reason rather than rethrown, because a job that vanished is
  indistinguishable from one still running, and one bad registration must not stop every later load.
- An optional daily refresh, off by default. It queues work rather than doing it, so a refresh goes
  through the same runner, the same one-at-a-time discipline and the same job rows as a load someone
  asked for. Staleness is measured from the last successful load, so a failing vehicle is retried on
  the schedule rather than hammered.
- The page shows a load's progress while it runs and the last few loads afterwards, marking a
  scheduled refresh that failed. A refresh failing silently overnight is how records quietly go stale.

- A second retrieval pool. `scope=campaigns` on `/api/search` ranks recalls and investigations
  alone, because a vehicle has thousands of complaints and a few dozen official records, and ranking
  them together hands every place to complaints. Measured on the loaded corpus, this takes recall@10
  from 0.125 to 0.400 and dense retrieval from 0.000 to 0.400 — same questions, same code, only the
  pool changed. A misspelled scope is a 400, never a silent fall back to searching everything.
- An answer now retrieves from both pools: the wider one for evidence, the campaign pool for the
  recalls and investigations panel. `POST /api/ask` returns those as `campaignMatches`, and the
  panel labels anything the answer did not quote as unverified. A retrieved record is a lead worth
  reading; a citation is a claim that survived checking. Collapsing the two would let the first
  borrow the credibility of the second.
- The evaluation scores every mode in both pools over identical cases, and the page shows the two
  side by side, because the gap between them is the finding. `eval/results.json` now names each
  entry's pool and carries a unique key per pool and mode, so a file read a year later is not
  ambiguous between two rows both labelled "sparse".
- The browser suite runs. A `UxFixture` environment seeds a throwaway database with the states the
  specs have to tell apart, and answers come from a scripted model rather than Claude, so a run
  costs nothing, finishes in seconds, and asserts the same thing every time. The scripted reply
  offers one quote that exists in a seeded record and one that does not, which exercises both
  halves of verification in a single run.
- The API serves the built web client from the same origin, so the page's own calls need no proxy.
  An unknown path under `/api` still answers 404 rather than handing back the page, which would
  leave a caller parsing HTML as though it were a result.
- A retrieval evaluation whose ground truth is derived rather than hand-labelled. NHTSA records
  which component an investigation concerns, when it was open, and which recall it produced, so the
  relevant records are the investigation and its recall and the queries are the complaints filed
  about that component while it was open. `eval` scores every retrieval mode over identical cases,
  stores the run, and writes `eval/results.json`. A mode that cannot run reports why rather than
  scoring zero, because zero reads as "this mode is bad" when it means "it was never tried".
- `GET /api/eval` returns the latest run and the runs behind it, with metrics handed back in the
  shape they were stored so an older run keeps its own fields. Before any run it returns null
  rather than a table of zeroes, which would read as a result.
- A README carrying the measured numbers, including what they do not measure, and why adding
  embeddings made the evaluation worse before a second pool made it better.
- A grounded answer to a symptom question. Retrieved records go to Claude with a schema that makes
  citations data rather than prose, and every quote is then checked character for character against
  the record it names. Quotes that fail are dropped and reported with the reason. An answer whose
  citations all fail is returned as not grounded, and the model's prose is discarded with them:
  text that reads as an answer will be read as one whatever flag sits beside it.
- `POST /api/ask` returns the answer, its verified citations with offsets into the record, the
  dropped ones with reasons, and any recall campaigns named. `GET /api/documents/{id}` returns a
  record verbatim so a quote can be highlighted inside it. Answering is registered only when a key
  exists, so without one the endpoint answers 503 and says search still works.
- An `embed` command back-fills embeddings for chunks that have none, in batches, saving each batch
  so a partial run resumes. Without a Voyage key it exits with `error: VOYAGE_API_KEY not set`
  rather than pretending to work.
- Hybrid retrieval over a vehicle's records, in three modes. Sparse matches keywords through the
  database's own full-text index, dense matches meaning through pgvector, and hybrid fuses the two
  with reciprocal rank fusion. Every hit carries the rank it held under each method and its fused
  score, so a reader can see whether a record surfaced by wording, by meaning, or by both. Sparse
  needs no embedding key and works on the records already loaded.
- `GET /api/vehicles` lists each registered vehicle with how many complaints, recalls and
  investigations it holds, and `GET /api/search` runs a search with optional component and filing
  date filters. An unknown vehicle answers 404, a rejected query 400, and a mode needing embeddings
  answers 409 naming keyword search as the alternative rather than failing.
- `GET /health` reports capability rather than liveness: whether the database is reachable and
  whether the embedding and answering keys are configured. A missing key is a normal operating
  state, and the client uses this to disable the modes it cannot offer.
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
- Cypress UX suite (`tests/ux/`) driven only by real pointer and keyboard events, with a support
  hook that refuses to run unless at least one vehicle has loaded complaints, so a green run
  cannot come from an empty database.

### Changed
- The test suites run on xunit v3 and Microsoft.Testing.Platform. Each test project is now its own
  executable that hosts the platform directly, so the separate `testhost.exe` process is gone
  entirely. On Windows that process opened a listening socket and prompted the developer through
  the firewall every time its path changed, which was once per git worktree. The stub HTTP server
  the ingest tests use now binds to loopback rather than every interface, for the same reason.
- Tests pass the run's own cancellation token to every asynchronous call, so cancelling a run stops
  it promptly instead of waiting on database and HTTP calls that were never told to stop.
- The database connection string and the local Postgres password no longer live in source or
  compose. They come from a gitignored `.env` (template in `.env.example`) or the environment,
  and the app, `dotnet ef` and compose all fail with a pointed message when they are missing.
  Prompted by a GitGuardian finding on the scaffold PR: the value was a throwaway local
  password, but a password in a repository is a habit worth not having.

### Fixed
- The evaluation table no longer breaks on a mode that did not run. The API wrapped each mode's
  numbers in an object the client did not expect, so it read a missing field and threw. Each mode
  now maps straight to its numbers or to null, with the reasons beside them, which is the shape the
  contract describes and the client was written against.
- An integration test asserted over the whole investigation-links table with no filter, so it
  passed alone and failed once other tests wrote links of their own. Scoped to its own vehicle.
- A question now matches records. Keyword search joined every word of the query with AND, so a
  real question ("I get a strong exhaust smell inside the cabin when I accelerate") matched nothing
  at all, while a three-word phrase happened to work. Terms are now joined with OR and ranked by
  coverage, which is what asking a question should do.
- A citation naming "RECORD 1886" is read as record 1886. The prompt heads each record with that
  label and the model reasonably echoed it, which discarded nine genuine citations in a live run.
  The quote itself is still checked character for character; only the prompt's own label is forgiven.
- A search that needs embeddings no longer returns 500 when the embedding provider is unreachable
  or erroring. The provider being down is a temporary, reportable state, distinct from having no
  key configured: the first answers 503 and the second 409, and both name keyword search as the
  alternative. Neither ever quotes the provider's response body, which can echo the credential.
- The unit-test timing gate no longer fails on a cold process. A 10 ms wall-clock threshold cannot
  tell first-use compilation from input and output, so one or two arbitrary tests failed on every
  cold run and passed on every warm one. Article V's 10 ms stays the recorded standard; the
  failing threshold is now a 100 ms ceiling that ordinary start-up never reaches, the assemblies
  whose loading caused the noise are loaded before the first timed test, and a structural test
  asserts the unit project references no driver that could reach outside the process.
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
