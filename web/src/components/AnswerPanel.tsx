// Presents a grounded answer: the text, whether any citation survived verification, how many were
// dropped, and the surviving citations as buttons that open the cited record.
import type { AskResponse, Citation } from "../api/client";

interface AnswerPanelProps {
  response: AskResponse;
  onOpenCitation: (citation: Citation) => void;
}

/** Wording for the dropped-citation count. The count is always shown, even when zero, so silence never hides a drop. */
export function describeDroppedCitations(droppedCitationCount: number): string {
  if (droppedCitationCount === 0) {
    return "Every citation the model offered was verified against its source record.";
  }
  const noun = droppedCitationCount === 1 ? "citation" : "citations";
  return `${droppedCitationCount} ${noun} dropped: the quoted text was not found in the record it cited.`;
}

export function AnswerPanel({ response, onOpenCitation }: AnswerPanelProps) {
  const groundedClass = response.isGrounded ? "grounded" : "not-grounded";
  return (
    <section className="panel" data-testid="answer-panel" data-grounded={response.isGrounded}>
      <span className={`status-pill ${groundedClass}`} data-testid="grounded-status">
        {response.isGrounded ? "Grounded" : "Not grounded"}
      </span>{" "}
      <span className="status-pill" data-testid="known-pattern-status">
        {response.isKnownPattern ? "Known pattern" : "No known pattern"}
      </span>
      <p data-testid="answer-text">{response.answer}</p>
      <p className="dropped-count" data-testid="dropped-count" data-dropped={response.droppedCitationCount}>
        {describeDroppedCitations(response.droppedCitationCount)}
      </p>
      {response.citations.length > 0 && (
        <ul className="citation-list" data-testid="citation-list">
          {response.citations.map((citation, index) => (
            <li key={`${citation.documentId}-${index}`}>
              <button type="button" data-testid="citation-button" onClick={() => onOpenCitation(citation)}>
                <span className="kind">{citation.kind}</span> {citation.externalId}
                <blockquote>{citation.quote}</blockquote>
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
