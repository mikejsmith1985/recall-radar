# Feature Specification: Recall Radar

**Feature Branch**: `001-recall-radar`

**Created**: 2026-09-03

**Status**: Draft

**Input**: User description: "Recall Radar — a hybrid-retrieval RAG app over NHTSA safety data that answers "my vehicle does X, is that a known problem, and is there a recall or investigation for it?" with every claim quoted verbatim from a real complaint, recall, or investigation record."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ask whether a symptom is a known problem (Priority: P1)

An owner picks one of their vehicles, describes a symptom in plain language ("exhaust smell in the cabin
when accelerating"), and receives a short answer stating whether this is a known pattern for that
vehicle, which recalls or investigations relate to it, and the exact records the answer rests on. Every
sentence of the answer that makes a claim is backed by a quote copied character-for-character from a
real NHTSA complaint, recall, or investigation record, and the owner can open each quoted record to read
the full text.

**Why this priority**: This is the product. Without a grounded answer there is nothing to show.

**Independent Test**: Can be fully tested by loading records for one vehicle, asking about a symptom
that matches a known investigation, and confirming the answer names that investigation and every quote
in it is found verbatim in the record it cites.

**Acceptance Scenarios**:

1. **Given** the 2013 Ford Explorer records are loaded, **When** the owner asks "exhaust smell inside the
   cabin", **Then** the answer identifies exhaust odor in the passenger compartment as a known pattern,
   lists investigation EA17-002, and every citation's quote appears verbatim in the cited record.
2. **Given** a loaded vehicle, **When** the owner asks about a symptom no record mentions, **Then** the
   answer states that no matching pattern was found in the loaded records and shows no citations.
3. **Given** an answer where the model produced a quote that is not in the cited record, **When** the
   answer is shown, **Then** that citation is removed, the removal is counted and visible to the owner,
   and if no citation survives the answer is labelled "not grounded" rather than presented as fact.

---

### User Story 2 - See why each record was retrieved (Priority: P2)

An owner (or a reviewer of the portfolio) searches a vehicle for a phrase and sees the matching
records ranked, with each result showing how it was found: by meaning (semantic match), by the words
used (keyword match), or by both. They can switch between meaning-only, keyword-only and combined
ranking and watch the ordering change.

**Why this priority**: The project exists to show how retrieval works, not just that it works. This
view is the teaching surface.

**Independent Test**: Search a loaded vehicle for a phrase in each of the three modes and confirm the
result list differs between modes and every result displays its rank under each method.

**Acceptance Scenarios**:

1. **Given** a loaded vehicle, **When** the owner searches "steering wheel locks up" in combined mode,
   **Then** each result shows its semantic rank, its keyword rank, and its combined score.
2. **Given** the same query, **When** the owner switches to keyword-only mode, **Then** the ordering
   changes and results are ranked by keyword relevance alone.
3. **Given** the same query, **When** the owner narrows to one component category, **Then** only
   records in that category are returned.

---

### User Story 3 - Measure retrieval quality against real ground truth (Priority: P3)

A reviewer opens the evaluation page and sees, for each retrieval mode, how often the records that
NHTSA itself linked to a defect investigation appear in the top results when the query is an owner
complaint about that defect. The numbers come from a repeatable run over the loaded data, not from
hand-picked examples, and the page shows how many of the answer citations survived verification.

**Why this priority**: It turns "the RAG works" into a measured claim. It depends on Stories 1 and 2
being in place.

**Independent Test**: Run the evaluation on loaded data and confirm it reports recall and reciprocal
rank per mode, a faithfulness figure, and that a re-run on unchanged data reproduces the same numbers.

**Acceptance Scenarios**:

1. **Given** loaded records that include at least one investigation with a linked recall campaign,
   **When** the evaluation runs, **Then** it reports recall@5, recall@10 and mean reciprocal rank for
   each of the three retrieval modes.
2. **Given** an evaluation run, **When** it is re-run on unchanged data, **Then** the reported numbers
   are identical.
3. **Given** the fixed question set, **When** the evaluation runs, **Then** it reports the share of
   citations that passed verbatim verification.

---

### User Story 4 - Load a vehicle's records (Priority: P4)

An owner adds a vehicle by year, make and model, and the system fetches that vehicle's complaints,
recalls and defect investigations from NHTSA, removing duplicates, and reports how many of each were
loaded.

**Why this priority**: Required by every other story, but it has no user-facing value on its own beyond
the counts, so it ranks last for demo purposes while being first in build order.

**Independent Test**: Load one vehicle and confirm the reported counts match the counts NHTSA returns
for the same vehicle on the same day, and that loading it again does not create duplicates.

**Acceptance Scenarios**:

1. **Given** no records, **When** the owner loads the 2013 Ford Explorer, **Then** complaints,
   recalls and investigations for that vehicle are stored and the counts are shown.
2. **Given** a vehicle whose NHTSA model name has several body-style variants returning identical
   complaint sets, **When** it is loaded, **Then** each complaint is stored once.
3. **Given** a loaded vehicle, **When** it is loaded again, **Then** the record counts do not change.

---

### Edge Cases

- A complaint summary that is empty or shorter than a sentence: stored, but never used as an
  evaluation query.
- A recall or investigation with no matching campaign number: kept as a record but excluded from the
  evaluation ground truth.
- NHTSA unreachable or rate-limiting during a load: the load stops with a clear message and stored data
  is left as it was before the load began.
- The owner asks a question with no vehicle selected: the system asks for a vehicle first.
- The answer service is unavailable or the embedding key is not configured: search still works in
  keyword-only mode and the owner is told why the other modes are off.
- Two vehicles loaded: a question about one never cites records belonging to the other.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let an owner register a vehicle by model year, make and model and load that
  vehicle's complaints, recalls and defect investigations from NHTSA's public data.
- **FR-002**: System MUST only ever read from NHTSA. No operation may create, change or delete anything
  outside the system's own store.
- **FR-003**: System MUST store each record once, keyed by its NHTSA identifier (complaint ODI number,
  recall campaign number, investigation action number), and repeat loads MUST NOT create duplicates.
- **FR-004**: System MUST store the full original text of every record and retain it unchanged; all
  verification is done against this stored text.
- **FR-005**: System MUST rank records for a vehicle by meaning, by keyword, and by a combination of
  the two, and MUST let the user choose the mode.
- **FR-006**: System MUST restrict every search and answer to the selected vehicle and MUST support
  narrowing by component category and by filing date range.
- **FR-007**: Every search result MUST show its rank under each retrieval method and its combined
  score.
- **FR-008**: System MUST produce an answer to a symptom question that states whether the symptom is a
  known pattern, lists related recalls and investigations, and cites the records it relies on.
- **FR-009**: Every citation MUST carry a quote that appears verbatim in the stored text of the cited
  record; only differences in whitespace are ignored. Any citation that fails this check MUST be
  removed before the answer is shown, and the number removed MUST be shown.
- **FR-010**: An answer with no surviving citation MUST be labelled "not grounded" and MUST NOT be
  presented as a factual finding.
- **FR-011**: System MUST let the user open the full text of any cited record with the quoted passage
  highlighted.
- **FR-012**: System MUST build its evaluation ground truth from NHTSA's own links between
  investigations and recall campaigns, never from hand-labelled examples.
- **FR-013**: System MUST report recall@5, recall@10 and mean reciprocal rank per retrieval mode, and
  the share of citations that passed verification, and repeated runs on unchanged data MUST give
  identical numbers.
- **FR-014**: System MUST keep secret keys out of its code, its logs and its stored data; they are
  supplied at run time by the operator's vault.
- **FR-015**: Loading, searching and answering MUST each report failures clearly and MUST leave stored
  data unchanged when they fail part-way.

### Key Entities *(include if feature involves data)*

- **Vehicle**: A model year, make and model the owner has registered, with the display name they know
  it by (e.g. "2014 F-150 SVT Raptor").
- **Source Record**: One complaint, recall or investigation from NHTSA, with its NHTSA identifier, the
  vehicle it belongs to, its component category, the date it was filed, and its full original text.
- **Retrievable Passage**: The searchable unit derived from a record. Most records are one passage;
  long investigations may be several. Always traceable back to its record.
- **Investigation Link**: The connection NHTSA records between an investigation and a recall campaign.
  This is the evaluation ground truth.
- **Answer**: A stored response to a question about a vehicle: the question, the answer text, the
  citations that survived verification, and how many were removed.
- **Evaluation Run**: One repeatable measurement of retrieval quality and citation faithfulness over the
  loaded data, with its numbers per retrieval mode.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of citations shown to a user pass verbatim verification against the stored record;
  zero unverified quotes are ever displayed.
- **SC-002**: For the 2013 Ford Explorer, asking about exhaust smell in the cabin surfaces investigation
  EA17-002 in the top five results in combined mode.
- **SC-003**: Combined-mode recall@10 on the derived ground truth is at least as high as the better of
  the two single modes.
- **SC-004**: Loading one vehicle's records (roughly 2,300 complaints, a dozen recalls and its
  investigations) completes in under 10 minutes on a home connection and reports counts matching
  NHTSA's for that day.
- **SC-005**: A search returns ranked results in under 2 seconds for a loaded vehicle.
- **SC-006**: Re-running the evaluation on unchanged data reproduces every reported number exactly.
- **SC-007**: A reviewer with no prior explanation can tell from the results view how each record was
  found (meaning, keyword or both).

## Assumptions

- The first two vehicles are the owner's own: 2013 Ford Explorer Sport and 2014 Ford F-150 SVT Raptor.
  NHTSA files the F-150 under several body-style model names that return identical complaint sets;
  the system treats them as one vehicle and removes duplicates by ODI number.
- NHTSA's public complaint and recall services and its downloadable investigations file are the only
  data sources. Technical service bulletins are out of scope for this feature.
- Single owner, local use; no accounts or multi-user permissions.
- Questions are asked in English.
- The semantic-ranking capability depends on an embedding provider key that is not yet available;
  until it is, keyword-only mode is the working mode and the other two modes are disabled with an
  explanation. Nothing else in the feature waits on that key.
- Records are refreshed only when the owner explicitly re-loads a vehicle; there is no scheduled sync.
