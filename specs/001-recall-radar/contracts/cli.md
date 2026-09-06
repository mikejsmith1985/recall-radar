# CLI Contract: `RecallRadar.Ingest`

Invoked as `dotnet run --project src/RecallRadar.Ingest -- <verb> [options]`. Connection string from
`RECALLRADAR_CONNECTION` (default: the docker-compose database). Exit code 0 on success, 1 on any
failure; failures print one line to stderr starting with `error:`.

## `ingest --vehicle <displayName> [--skip-embed]`

Loads complaints, recalls and investigations for a registered vehicle, in one transaction.

Output (stdout, one line each):
```
vehicle: 2013 Explorer Sport (FORD 2013, models: EXPLORER)
complaints: fetched 2231, new 2231, unchanged 0
recalls: fetched 12, new 12, unchanged 0
investigations: rows 4, new 4, links 2
chunks: created 2251, embedded 0 (embeddings unavailable: VOYAGE_API_KEY not set)
done in 00:01:42
```

Rules: read-only against NHTSA; dedupe complaints on `odiNumber` across all model names; a network
failure aborts the transaction and prints `error:`; `--skip-embed` or a missing key leaves
`embedding` null.

## `embed [--vehicle <displayName>]`

Back-fills `embedding` for chunks where it is null, in batches of ≤ 128. Prints
`chunks: embedded N, remaining 0`. Exit 1 with `error: VOYAGE_API_KEY not set` when unavailable.

## `eval [--vehicle <displayName>]`

Builds ground truth from `investigation_links`, then runs every case through every mode in **both
pools** — all records, and recalls plus investigations alone. Computes recall@5, recall@10 and MRR
per combination, and stores an `evaluation_runs` row. Also writes `eval/results.json`, committed as
the README's evidence, where each entry carries a `scope` name and a unique `key`.

Output:
```
cases: 80

pool       mode      recall@5 recall@10       mrr    scored
-----------------------------------------------------------
all        sparse       0.075     0.125     0.057        80
all        dense        0.000     0.000     0.000        80
all        hybrid       0.000     0.000     0.000        80
campaigns  sparse       0.375     0.375     0.375        80
campaigns  dense        0.325     0.400     0.283        80
campaigns  hybrid       0.375     0.400     0.279        80

done in 00:00:25
written: <repo>/eval/results.json
```
Modes needing embeddings are skipped with a note when unavailable. Re-running on unchanged data must
reproduce identical numbers (SC-006).

## `vehicles list` / `vehicles add --make Ford --year 2014 --name "2014 F-150 SVT Raptor" --models "F-150 SUPER CREW,F-150 SUPERCAB,F-150 REGULAR CAB"`

Registers a vehicle after validating each model name against NHTSA's models endpoint. The `add` verb
is the CLI twin of `POST /api/vehicles`.
