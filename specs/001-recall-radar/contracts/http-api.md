# HTTP API Contract: Recall Radar

Base path `/api`. JSON in and out. All responses carry `Content-Type: application/json`.
Errors use RFC 9457 problem details (`application/problem+json`).

## GET /health

`200 { "status": "ok", "database": "ok", "embeddings": "ok" | "unavailable", "answering": "ok" | "unavailable" }`

`embeddings` is `unavailable` when no `VOYAGE_API_KEY` is configured; `answering` when no
`ANTHROPIC_API_KEY` is configured.

## GET /api/vehicles

`200 [ { "id": 1, "displayName": "2013 Explorer Sport", "make": "FORD", "modelYear": 2013,
"counts": { "complaint": 2231, "recall": 12, "investigation": 4 } } ]`

## POST /api/vehicles

Request: `{ "make": "Ford", "nhtsaModel": "EXPLORER", "recallModel": null, "modelYear": 2013,
"displayName": "2013 Explorer Sport" }`

`recallModel` is only needed where NHTSA's two feeds disagree: complaints are filed against a body
style (`F-150 SUPER CREW`) and recalls answer that with 400, so a truck needs both names.

A load reaches three NHTSA feeds and takes minutes, so the request cannot wait for it. The endpoint
validates the model name against NHTSA's own list, queues the work and answers immediately.

- `202` with a load body (below) and a `Location` of `/api/loads/{id}`. Asking twice for a vehicle
  whose load is still running returns that same load rather than starting a second pass.
- `400` problem when the registration could never be looked up, or when the model name is not in
  NHTSA's list for that make and year — the detail names up to 20 valid options.
- `503` problem when NHTSA's model list cannot be reached. The feed being down is not the caller's
  mistake, so it is not a 400.

## POST /api/vehicles/{id}/refresh

Queues another pass over the feeds for a vehicle that already exists. Same `202` and same joining
behaviour as above. `404` when the vehicle is unknown.

## GET /api/loads/{id} · GET /api/loads

One load, or the most recent 20, newest first.

```json
{
  "id": 12, "vehicleId": 3, "displayName": "2013 Explorer Sport",
  "state": "succeeded", "trigger": "manual", "isFinished": true,
  "queuedAt": "2026-09-06T12:00:00Z", "startedAt": "2026-09-06T12:00:02Z",
  "finishedAt": "2026-09-06T12:03:00Z", "message": null,
  "report": { "complaintsNew": 2231, "recallsNew": 12, "investigationsNew": 6,
              "chunksCreated": 2249, "chunksEmbedded": 2249 }
}
```

`state` is `queued`, `running`, `succeeded` or `failed`; `trigger` is `manual` or `scheduled`.
`message` carries the reason a load failed and is null otherwise — a load that vanished would be
indistinguishable from one still running. `report` is null until a load succeeds, and keeps whatever
shape the version that wrote it used. `404` for a load that does not exist.

Loading is also available at the command line (`ingest`); see [cli.md](cli.md).

## GET /api/search

Query: `vehicleId` (required), `q` (required, 2–500 chars), `mode` = `dense|sparse|hybrid` (default
`hybrid`), `scope` = `all|campaigns` (default `all`), `component` (optional exact match),
`filedFrom`/`filedTo` (optional ISO dates), `limit` (default 10, max 50).

`scope=campaigns` ranks only recalls and investigations. It exists because a vehicle has thousands
of complaints and a few dozen official records, so ranking them together gives every place to
complaints. Measured effect on this corpus: recall@10 0.125 → 0.400.

`200`:
```json
{
  "mode": "hybrid",
  "scope": "all",
  "hits": [
    {
      "documentId": 4123, "chunkId": 4123, "kind": "investigation",
      "externalId": "EA17002", "title": "Exhaust Odor in Passenger Cab",
      "component": "ENGINE AND ENGINE COOLING:EXHAUST SYSTEM", "filedOn": "2017-07-27",
      "snippet": "During the EA17-002 investigation, the agency reviewed …",
      "denseRank": 1, "sparseRank": 3, "fusedScore": 0.03226
    }
  ]
}
```
`denseRank`/`sparseRank` are `null` when the hit did not appear in that method's top 50 or the mode
excluded it.

- `409` problem `"Embeddings are unavailable; use mode=sparse"` for `dense`/`hybrid` when no
  embedding key is configured or the vehicle has no embedded chunks.
- `404` when `vehicleId` is unknown.
- `400` problem `"Unknown scope '<value>'. Use all or campaigns."` — a misspelled scope is an error,
  never a silent fall back to `all`, which would read as the campaign pool being empty.

## POST /api/ask

Request: `{ "vehicleId": 1, "question": "exhaust smell inside the cabin", "mode": "hybrid" }`

`200`:
```json
{
  "answerId": 17,
  "answer": "Exhaust odor entering the passenger compartment is a documented pattern …",
  "isKnownPattern": true,
  "isGrounded": true,
  "citations": [
    { "documentId": 4123, "externalId": "EA17002", "kind": "investigation",
      "quote": "the agency reviewed and analyzed", "startOffset": 41, "endOffset": 73 }
  ],
  "campaignMatches": [
    { "documentId": 4123, "kind": "investigation", "externalId": "EA17002",
      "title": "Exhaust Odor in Passenger Cab",
      "component": "ENGINE AND ENGINE COOLING:EXHAUST SYSTEM", "filedOn": "2017-07-27" }
  ],
  "droppedCitationCount": 1,
  "linkedCampaigns": ["17V000000"],
  "retrievedDocumentIds": [4123, 2210, 2287]
}
```
Rules: every returned citation passed verification; `droppedCitationCount` is the number removed;
`isGrounded=false` whenever `citations` is empty, and then `answer` explains why (no citations
survived / model refused / model unavailable). Never `500` for a model-side failure.

- `503` problem when `ANTHROPIC_API_KEY` is not configured.

## GET /api/documents/{id}

`200 { "id": 4123, "kind": "investigation", "externalId": "EA17002", "title": "...", "component": "...",
"filedOn": "2017-07-27", "body": "<verbatim>", "vehicleId": 1 }` — the UI highlights
`[startOffset, endOffset)` from a citation inside `body`.

## GET /api/eval

`200 { "latest": { "id": 3, "ranAt": "...", "caseCount": 41,
"metrics": { "dense": {...}, "sparse": {...}, "hybrid": {...}, "faithfulness": { "emitted": 60, "verified": 58 } } },
"history": [ ... ] }` — `latest` is `null` before any run. Running the evaluation is a CLI verb.
