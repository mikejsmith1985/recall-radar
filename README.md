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

3,634 retrievable passages in total, every one embedded with `voyage-3.5` at 1024 dimensions.

## Retrieval quality, measured

Ground truth is derived, never hand-labelled. NHTSA opens an investigation, records the component
and the dates, and names the recall campaign it produced. So for each investigation the relevant
records are the investigation and its recall, and the queries are the complaints about that
component filed while it was open. Those are the complaints the investigation was actually about.

Run with `dotnet run --project src/RecallRadar.Ingest -- eval`. Results land in `eval/results.json`.

**80 cases, three modes, two pools:**

| Pool | Mode | recall@5 | recall@10 | MRR |
|---|---|---|---|---|
| all records | sparse | 0.075 | 0.125 | 0.057 |
| all records | dense | 0.000 | 0.000 | 0.000 |
| all records | hybrid | 0.000 | 0.000 | 0.000 |
| recalls and investigations | sparse | 0.375 | 0.375 | **0.375** |
| recalls and investigations | dense | 0.325 | **0.400** | 0.283 |
| recalls and investigations | hybrid | 0.375 | **0.400** | 0.279 |

### Reading that honestly

The top three rows are bad, and they are the real numbers. Ranking every record together, keyword
search finds the governing investigation within ten results about one time in eight, and semantic
search never finds it at all.

The cause is arithmetic, not ranking. The corpus is 3,593 complaints against 20 recalls and 8
investigations. A query written in a complaint's own words matches other complaints, which are
plentiful and phrased alike. Ranking the exhaust-odour investigation against a genuinely relevant
exhaust complaint puts it **774th**.

Hybrid scoring 0.000 while keyword scores 0.125 is not a bug either. Reciprocal rank fusion rewards
agreement, so a record both methods found beats one only keyword found — and the complaints are what
both methods find. Fusion actively destroys keyword's occasional lucky hit.

The bottom three rows are the same questions against the same code, with only the pool changed.
Scoping retrieval to recalls and investigations takes recall@10 from **0.125 to 0.400**, and takes
dense from **0.000 to 0.400**. Semantic retrieval was never bad at meaning here; it was drowning.

One more thing sits in those numbers. Keyword has the best MRR (0.375) while dense has the best
recall@10 (0.400), and keyword's three figures are identical — when keyword finds the right campaign
it puts it at rank 1, whereas dense finds it more often but ranks it lower. Fusing the two buys
recall and costs precision at the top. That is a trade, not a win.

It is worth being clear about what none of this measures. Finding many similar complaints is useful
on its own: it is what establishes a symptom as a pattern, and it is what produced the nine-citation
answer above. This metric asks the narrower question of whether the official record surfaces, and
before the second pool existed it answered unflatteringly.

Both pools are live in the product. An answer retrieves from the wider pool for evidence and from
the campaign pool for the recalls and investigations panel, which labels anything it did not quote
as unverified — a retrieved record is a lead to read, a citation is a claim that survived checking.

## Running it

```bash
cp .env.example .env          # fill in a local database password
docker compose up -d          # PostgreSQL 17 with pgvector
scripts/run-dev-clean.ps1     # the API and web client
```

Add a vehicle in the app: give it a make, the model name NHTSA files records under, a year, and
whatever you call it. The records load in the background and the page shows how far it has got. A
model name NHTSA does not recognise is rejected with the names it does recognise, which is the
mistake people actually make.

The command line does the same work, and is still the right tool for a scripted load:

```bash
dotnet run --project src/RecallRadar.Ingest -- ingest --vehicle "2013 Explorer Sport"
dotnet run --project src/RecallRadar.Ingest -- eval
```

Set `ScheduledRefresh:IsEnabled` to turn on a daily pass that picks up newly filed complaints. It is
off by default, because a background process that reaches the internet should be opted into.

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
- Adding embeddings made the evaluation **worse**, not better: dense and hybrid both scored zero
  where keyword scored 0.125. Nothing was broken. Retrieval was ranking 28 official records against
  3,593 complaints, and the complaints won every place. The fix was a second pool, not a better model.
