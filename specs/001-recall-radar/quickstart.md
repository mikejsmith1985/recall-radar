# Quickstart: Recall Radar

Validation guide. Every step names the evidence that proves it worked (Article X).

## Prerequisites

- .NET SDK 10.0.400 (`dotnet --version`)
- Docker Desktop running (`docker ps`)
- Node 24 (`node --version`) for the web client and Cypress
- Forge Terminal with the vault unlocked. Secrets are injected, never pasted:
  `ANTHROPIC_API_KEY` ← the Anthropic key entry in the Forge Vault; `VOYAGE_API_KEY` ← (pending).

## 1. Database

```powershell
docker compose up -d
dotnet tool restore
dotnet ef database update --project src/RecallRadar.Retrieval --startup-project src/RecallRadar.Api
```
**Expect**: `docker compose ps` shows `recall-radar-postgres` healthy; `\dx` in psql lists `vector`.

## 2. Register and load a vehicle

```powershell
dotnet run --project src/RecallRadar.Ingest -- vehicles add --make Ford --year 2013 --name "2013 Explorer Sport" --models "EXPLORER"
dotnet run --project src/RecallRadar.Ingest -- ingest --vehicle "2013 Explorer Sport"
```
**Expect** (counts as of 2026-09-03; they grow over time): `complaints: fetched 2231`,
`recalls: fetched 12`, `investigations: rows 4`. Running `ingest` again prints `new 0` for every kind.
Repeat for `--make Ford --year 2014 --name "2014 F-150 SVT Raptor" --models "F-150 SUPER CREW,F-150 SUPERCAB,F-150 REGULAR CAB"`: `complaints: fetched 1362` (deduped across the three names).

## 3. Search (keyword mode works without any key)

```powershell
.\scripts\run-dev-clean.ps1            # starts the API on http://127.0.0.1:5180, records the PID
curl "http://127.0.0.1:5180/api/search?vehicleId=1&q=exhaust%20smell%20in%20cabin&mode=sparse"
```
**Expect**: JSON `hits` including `externalId: "EA17002"` with a non-null `sparseRank`.
With `mode=hybrid` before the Voyage key exists: HTTP 409 with the reason in `detail`.

## 4. Embed and compare modes (after `VOYAGE_API_KEY` is in the vault)

```powershell
dotnet run --project src/RecallRadar.Ingest -- embed
curl ".../api/search?vehicleId=1&q=exhaust%20smell%20in%20cabin&mode=dense"
curl ".../api/search?vehicleId=1&q=exhaust%20smell%20in%20cabin&mode=hybrid"
```
**Expect**: EA17002 in the top 5 for `hybrid` (SC-002); the three orderings differ; every hit shows
`denseRank`, `sparseRank`, `fusedScore`.

## 5. Ask

```powershell
curl -X POST http://127.0.0.1:5180/api/ask -H "Content-Type: application/json" -d '{"vehicleId":1,"question":"exhaust smell inside the cabin","mode":"hybrid"}'
```
**Expect**: `isGrounded: true`, `isKnownPattern: true`, at least one citation whose `quote` you can
find verbatim (ignoring whitespace) in `GET /api/documents/{documentId}`. `droppedCitationCount`
is reported, not hidden.

## 6. Evaluate

```powershell
dotnet run --project src/RecallRadar.Ingest -- eval
```
**Expect**: the table from [contracts/cli.md](contracts/cli.md); `eval/results.json` written; a second
run reproduces the numbers exactly (SC-006); hybrid recall@10 ≥ max(dense, sparse) (SC-003) — record
the real numbers in the README whatever they are.

## 7. Tests

```powershell
dotnet test tests/RecallRadar.Unit          # every test < 10 ms or the run fails
dotnet test tests/RecallRadar.Integration   # starts a pgvector container; no live network
.\scripts\run-dev-clean.ps1 -CypressOnly    # seeds a fixture DB, runs Cypress with real events, stops by PID
cd web; npm test                            # Vitest component tests
```
**Expect**: all green, and the Cypress support hook fails fast if pointed at an empty database.

## 8. Stop

```powershell
.\scripts\run-dev-clean.ps1 -Stop
```
**Expect**: `Stopped process <pid>.` — by PID only, never by name.
