// Shows a source record in full with one verified citation highlighted at its exact character range.
// The highlight uses the API's offsets into the stored body, so what the owner sees marked is
// exactly the span the verifier matched.
import type { Citation, SourceDocument } from "../api/client";

interface CitationViewProps {
  document: SourceDocument;
  citation: Citation;
}

export interface HighlightSegments {
  before: string;
  highlighted: string;
  after: string;
}

/** Splits the body around [startOffset, endOffset). Out-of-range offsets fall back to no highlight. */
export function splitForHighlight(body: string, startOffset: number, endOffset: number): HighlightSegments {
  const isRangeValid = startOffset >= 0 && endOffset <= body.length && startOffset < endOffset;
  if (!isRangeValid) {
    return { before: body, highlighted: "", after: "" };
  }
  return {
    before: body.slice(0, startOffset),
    highlighted: body.slice(startOffset, endOffset),
    after: body.slice(endOffset),
  };
}

export function CitationView({ document, citation }: CitationViewProps) {
  const segments = splitForHighlight(document.body, citation.startOffset, citation.endOffset);

  return (
    <section className="panel" data-testid="citation-view" aria-label={`Record ${document.externalId}`}>
      <h3>
        <span className="kind">{document.kind}</span> {document.title || document.externalId}
      </h3>
      <div className="meta">
        {document.externalId} · {document.component || "component unknown"} · {document.filedOn ?? "date unknown"}
      </div>
      <pre className="record-body" data-testid="record-body">
        {segments.before}
        {segments.highlighted && <mark data-testid="citation-highlight">{segments.highlighted}</mark>}
        {segments.after}
      </pre>
    </section>
  );
}
