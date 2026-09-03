// One search hit, with the explanation of how it was found: its rank by meaning, by keyword, and the fused score.
import type { SearchHit } from "../api/client";

interface ResultCardProps {
  hit: SearchHit;
  position: number;
}

const FusedScoreDecimals = 4;

/** Formats a per-method rank, or says the method did not surface this record at all. */
export function describeRank(methodName: string, rank: number | null): string {
  return rank === null ? `${methodName}: not in top results` : `${methodName} rank ${rank}`;
}

export function formatFiledOn(filedOn: string | null): string {
  return filedOn ?? "date unknown";
}

export function ResultCard({ hit, position }: ResultCardProps) {
  return (
    <li className="result-card" data-testid="result-card" data-external-id={hit.externalId}>
      <header>
        <div>
          <span className="kind">{hit.kind}</span>{" "}
          <strong>{hit.title || hit.externalId}</strong>
        </div>
        <span className="meta">#{position}</span>
      </header>
      <div className="meta">
        {hit.externalId} · {hit.component || "component unknown"} · {formatFiledOn(hit.filedOn)}
      </div>
      <p>{hit.snippet}</p>
      <div className="rank-explanation" data-testid="rank-explanation">
        <span className={`rank-badge${hit.denseRank === null ? " absent" : ""}`} data-testid="dense-rank">
          {describeRank("Meaning", hit.denseRank)}
        </span>
        <span className={`rank-badge${hit.sparseRank === null ? " absent" : ""}`} data-testid="sparse-rank">
          {describeRank("Keyword", hit.sparseRank)}
        </span>
        <span className="rank-badge" data-testid="fused-score">
          Fused score {hit.fusedScore.toFixed(FusedScoreDecimals)}
        </span>
      </div>
    </li>
  );
}
