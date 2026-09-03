# Data Model: Recall Radar

**Date**: 2026-09-03 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

All tables live in one PostgreSQL schema managed by EF Core migrations in
`src/RecallRadar.Retrieval/Migrations/`. Names are snake_case in the database and PascalCase in C#.

## vehicles

| Column | Type | Rules |
|---|---|---|
| id | int PK identity | |
| make | text | upper-cased as NHTSA stores it (`FORD`) |
| model_year | int | 1949 ≤ year ≤ current + 1 |
| display_name | text | owner-facing, e.g. `2014 F-150 SVT Raptor` |
| nhtsa_model_names | text[] | every NHTSA model string to query (`{"F-150 SUPER CREW","F-150 SUPERCAB","F-150 REGULAR CAB"}`); validated against the models endpoint at registration |
| created_at | timestamptz | |

Unique: (`make`, `model_year`, `display_name`).

## source_documents

One row per NHTSA record. `body` is the verbatim text the quote verifier checks against.

| Column | Type | Rules |
|---|---|---|
| id | bigint PK identity | the `documentId` sent to the model |
| vehicle_id | int FK → vehicles | every search filters on this |
| kind | text enum `complaint` / `recall` / `investigation` | |
| external_id | text | `odiNumber` / `NHTSACampaignNumber` / investigation `action_no` |
| component | text | NHTSA component string, e.g. `STEERING`, `ENGINE AND ENGINE COOLING:EXHAUST SYSTEM` |
| filed_on | date nullable | complaint `dateComplaintFiled`, recall `ReportReceivedDate`, investigation `open_date` |
| title | text | complaint: first 80 chars of summary; recall: campaign + component; investigation: `subject` |
| body | text | complaint: `summary`; recall: `Summary\n\nConsequence\n\nRemedy`; investigation: `summary` |
| raw_json | jsonb | the original record for audit and re-chunking |
| ingested_at | timestamptz | |

Unique: (`vehicle_id`, `kind`, `external_id`) — this is what makes repeat loads idempotent (FR-003).
Index: (`vehicle_id`, `kind`), (`vehicle_id`, `component`), (`vehicle_id`, `filed_on`).

## document_chunks

The retrievable passage. Complaints and recalls are one chunk each; investigation summaries are split
at paragraph boundaries into ≤ 1,500-character chunks.

| Column | Type | Rules |
|---|---|---|
| id | bigint PK identity | |
| document_id | bigint FK → source_documents (cascade delete) | |
| ordinal | int | 0-based position within the document |
| text | text | the passage |
| embedding | vector(1024) nullable | null until embedded (key pending) |
| search_text | tsvector generated stored `to_tsvector('english', text)` | |

Unique: (`document_id`, `ordinal`).
Indexes: HNSW on `embedding vector_cosine_ops`; GIN on `search_text`; btree on `document_id`.

## investigation_links

The evaluation ground truth: which recall campaign an investigation led to.

| Column | Type | Rules |
|---|---|---|
| investigation_document_id | bigint FK → source_documents | must be `kind = investigation` |
| campaign_number | text | from `FLAT_INV.campaign_no`; rows with empty campaign are not linked |
| component | text | the investigation's component for that vehicle row |

PK: (`investigation_document_id`, `campaign_number`, `component`).

## answers

| Column | Type | Rules |
|---|---|---|
| id | bigint PK identity | |
| vehicle_id | int FK → vehicles | |
| question | text | |
| retrieval_mode | text enum `dense` / `sparse` / `hybrid` | |
| answer_text | text | |
| is_known_pattern | bool | |
| is_grounded | bool | false when no citation survived |
| citations_json | jsonb | surviving citations: `[{documentId, quote, startOffset, endOffset}]` |
| dropped_citation_count | int | |
| linked_campaigns | text[] | |
| model | text | e.g. `claude-opus-5` |
| created_at | timestamptz | |

## evaluation_runs

| Column | Type | Rules |
|---|---|---|
| id | bigint PK identity | |
| vehicle_id | int FK → vehicles nullable | null = all vehicles |
| ran_at | timestamptz | |
| case_count | int | number of (query, relevant-set) pairs |
| metrics_json | jsonb | `{ "dense": {recallAt5, recallAt10, mrr}, "sparse": {...}, "hybrid": {...}, "faithfulness": {emitted, verified} }` |

## Domain types (C#, `RecallRadar.Domain`, no persistence attributes)

- `VehicleIdentity(Make, ModelYear, NhtsaModelNames)` — value object.
- `RecordKind` enum — `Complaint`, `Recall`, `Investigation`.
- `RetrievalMode` enum — `Dense`, `Sparse`, `Hybrid`.
- `RankedHit(DocumentId, ChunkId, DenseRank?, SparseRank?, FusedScore)`.
- `ReciprocalRankFusion.Fuse(denseOrder, sparseOrder, k = 60)` → ordered `RankedHit` list.
- `Citation(DocumentId, Quote)`; `VerifiedCitation(Citation, StartOffset, EndOffset)`.
- `QuoteVerifier.Verify(citations, bodiesById)` → `(verified, dropped, reasons)`; whitespace-normalised
  ordinal substring, exact port of Randomness `_appears_verbatim`.
- `GroundedAnswer(AnswerText, IsKnownPattern, IsGrounded, VerifiedCitations, DroppedCitationCount, LinkedCampaigns)`.
- `GroundTruthCase(QueryText, RelevantDocumentIds)`; `RetrievalMetrics.Compute(cases, rankingsByCase)` →
  `(RecallAt5, RecallAt10, Mrr)`.

## State transitions

- **Ingest**: `fetch → parse → upsert source_documents → chunk → (embed if generator available) → link investigations`. Runs inside one transaction per vehicle so a failed load leaves the previous state intact (FR-015).
- **Answer**: `retrieve → prompt → parse structured output → verify citations → persist answer`. An unparseable or refused response persists as `is_grounded = false` with `answer_text` explaining why.
