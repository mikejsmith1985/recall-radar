// Lists the recalls and investigations an answer tied the symptom to, so the owner can act on them.
import type { AskResponse } from "../api/client";

interface KnownPatternPanelProps {
  response: AskResponse;
}

/** Unique cited investigations and recalls, in citation order; complaints are evidence, not actions. */
export function collectCitedRecords(response: AskResponse): { kind: string; externalId: string }[] {
  const seen = new Set<string>();
  const records: { kind: string; externalId: string }[] = [];
  for (const citation of response.citations) {
    const isActionable = citation.kind === "recall" || citation.kind === "investigation";
    if (!isActionable || seen.has(citation.externalId)) {
      continue;
    }
    seen.add(citation.externalId);
    records.push({ kind: citation.kind, externalId: citation.externalId });
  }
  return records;
}

export function KnownPatternPanel({ response }: KnownPatternPanelProps) {
  const citedRecords = collectCitedRecords(response);
  const hasAnything = response.linkedCampaigns.length > 0 || citedRecords.length > 0;

  return (
    <section className="panel known-pattern-panel" data-testid="known-pattern-panel">
      <h3>Related recalls and investigations</h3>
      {!hasAnything && (
        <p className="empty-state" data-testid="known-pattern-empty">
          No recall campaign or investigation was linked to this answer.
        </p>
      )}
      {response.linkedCampaigns.length > 0 && (
        <>
          <strong>Recall campaigns</strong>
          <ul data-testid="linked-campaigns">
            {response.linkedCampaigns.map((campaign) => (
              <li key={campaign}>{campaign}</li>
            ))}
          </ul>
        </>
      )}
      {citedRecords.length > 0 && (
        <>
          <strong>Cited records</strong>
          <ul data-testid="cited-records">
            {citedRecords.map((record) => (
              <li key={record.externalId}>
                <span className="kind">{record.kind}</span> {record.externalId}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
