// Lists the recalls and investigations an answer tied the symptom to, so the owner can act on them.
import type { AskResponse, CampaignMatch } from "../api/client";

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

/**
 * Campaign-pool matches the answer did not quote. Shown separately and labelled, because a record
 * that was retrieved is a lead to read, while a citation is a claim that survived verification.
 * Collapsing the two would let an unverified record borrow the credibility of a verified one.
 */
export function collectUncitedMatches(response: AskResponse): CampaignMatch[] {
  const cited = new Set(collectCitedRecords(response).map((record) => record.externalId));
  return (response.campaignMatches ?? []).filter((match) => !cited.has(match.externalId));
}

export function KnownPatternPanel({ response }: KnownPatternPanelProps) {
  const citedRecords = collectCitedRecords(response);
  const uncitedMatches = collectUncitedMatches(response);
  const hasAnything =
    response.linkedCampaigns.length > 0 || citedRecords.length > 0 || uncitedMatches.length > 0;

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
      {uncitedMatches.length > 0 && (
        <>
          <strong>Also matched, not quoted</strong>
          <p className="caption" data-testid="uncited-caption">
            Retrieved from this vehicle&apos;s recalls and investigations. Nothing here was quoted in
            the answer, so none of it has been verified — read it yourself.
          </p>
          <ul data-testid="uncited-matches">
            {uncitedMatches.map((match) => (
              <li key={match.documentId}>
                <span className="kind">{match.kind}</span> {match.externalId} — {match.title}
              </li>
            ))}
          </ul>
        </>
      )}
    </section>
  );
}
