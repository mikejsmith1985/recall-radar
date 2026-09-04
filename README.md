# Recall Radar

Ask whether a symptom in your car is a known problem, and get an answer whose every claim is a
quote that has been checked against the record it came from.

The corpus is public NHTSA data for two specific vehicles: owner complaints, recall campaigns, and
the defect investigations that connect them. Nothing is uploaded and nothing is hand-labelled.

## What it does

Ask a question in your own words. Recall Radar retrieves records for that vehicle, sends them to
Claude with a schema that makes citations data rather than prose, then checks every quote character
for character against the record it names. A quote that does not appear is dropped and counted. An
answer whose citations all fail is returned as **not grounded**, and the model's prose is discarded
with it, because text that reads as an answer will be read as one whatever flag sits beside it.

Asked *"I get a strong exhaust smell inside the cabin when I accelerate. Is this a known problem?"*
about a 2013 Explorer, it answers from nine verified citations: owners describing nausea and
headaches, a dealer performing service bulletin 12-12-4 without fixing it, and cracked welds found
between the exhaust pipes and the resonator. Every one of those traces to a real complaint at a
known character offset.

## What is loaded

| Vehicle | Complaints | Recalls | Investigations |
|---|---|---|---|
| 2013 Explorer Sport | 2,231 | 12 | 6 |
| 2014 F-150 SVT Raptor | 1,362 | 8 | 2 |

3,634 retrievable passages in total.

## Retrieval quality, measured

Ground truth is derived, never hand-labelled. NHTSA opens an investigation, records the component
and the dates, and names the recall campaign it produced. So for each investigation the relevant
records are the investigation and its recall, and the queries are the complaints about that
component filed while it was open. Those are the complaints the investigation was actually about.

Run with `dotnet run --project src/RecallRadar.Ingest -- eval`. Results land in `eval/results.json`.

**80 cases, keyword retrieval only:**

| Mode | recall@5 | recall@10 | MRR |
|---|---|---|---|
| sparse | 0.075 | 0.125 | 0.057 |
| dense | not run | not run | not run |
| hybrid | not run | not run | not run |

### Reading that honestly

Keyword retrieval surfaces the governing investigation within ten results about one time in eight.
That is a poor score and it is the real one.

The cause is visible in the corpus: 2,231 complaints against 6 investigations. A query written in a
complaint's own words matches other complaints, which are plentiful and phrased alike, so the one
authoritative record is buried under them. This is precisely the gap semantic retrieval is supposed
to close, and the number above is the baseline it has to beat.

It is worth being clear about what this does **not** measure. Finding many similar complaints is
useful on its own: that is what establishes a symptom as a pattern, and it is what produced the
nine-citation answer above. This metric asks a narrower question, whether retrieval surfaces the
investigation or recall specifically, and answers it unflatteringly.

Dense and hybrid are built and tested but report **not run** rather than zero, because no embedding
key is configured yet. A zero would read as "hybrid is bad" when it means "hybrid was never tried".

## Running it

```bash
cp .env.example .env          # fill in a local database password
docker compose up -d          # PostgreSQL 17 with pgvector
dotnet run --project src/RecallRadar.Ingest -- ingest --vehicle "2013 Explorer Sport"
dotnet run --project src/RecallRadar.Ingest -- eval
scripts/run-dev-clean.ps1     # the API and web client
```

Answering needs `ANTHROPIC_API_KEY`; dense retrieval needs `VOYAGE_API_KEY`. Without either, keyword
search and every record stay fully available, and `/health` says which features are live.

## How it is built

.NET 10 minimal API, PostgreSQL with pgvector for embeddings and its own full-text index for
keywords, fused with reciprocal rank fusion. React and TypeScript for the client. The Anthropic C#
SDK with structured output for answering.

Three test layers, per the project constitution: unit tests that cannot reference a driver,
integration tests against a real database container and stubbed HTTP, and Cypress driving real
browser events.

## Things the live data taught

Every one of these passed its tests and still failed on real data.

- Keyword search joined every query word with **AND**, so a whole question matched nothing while a
  three-word phrase worked fine.
- The prompt heads each record `RECORD 1886`, and the model reasonably cited that whole label as the
  id, discarding nine genuine citations.
- NHTSA's complaints and recalls feeds disagree about model names: complaints want
  `F-150 SUPER CREW`, and recalls answer that with 400 Bad Request.
- The API and the ingest command both shipped a file called `appsettings.json`, so whichever built
  last silently won in a shared output folder.
