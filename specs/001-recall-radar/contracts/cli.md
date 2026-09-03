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

## `eval [--vehicle <displayName>] [--modes dense,sparse,hybrid] [--questions eval/questions.json]`

Builds ground truth from `investigation_links`, runs every case in every requested mode, computes
recall@5, recall@10 and MRR, runs the faithfulness question set through `/ask` logic, and stores an
`evaluation_runs` row. Also writes `eval/results.json` (committed as the README evidence).

Output:
```
cases: 41 (investigations with campaigns: 2)
mode      recall@5  recall@10  mrr
dense     0.732     0.854      0.611
sparse    0.683     0.780      0.552
hybrid    0.805     0.902      0.667
faithfulness: 58/60 citations verified
run id: 3
```
Modes needing embeddings are skipped with a note when unavailable. Re-running on unchanged data must
reproduce identical numbers (SC-006).

## `vehicles list` / `vehicles add --make Ford --year 2014 --name "2014 F-150 SVT Raptor" --models "F-150 SUPER CREW,F-150 SUPERCAB,F-150 REGULAR CAB"`

Registers a vehicle after validating each model name against NHTSA's models endpoint. The `add` verb
is the CLI twin of `POST /api/vehicles`.
