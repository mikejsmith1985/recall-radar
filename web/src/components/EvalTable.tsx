// Renders one evaluation run: recall@5, recall@10 and MRR per retrieval mode, plus citation faithfulness.
import type { EvalRun, ModeMetrics, RetrievalMode } from "../api/client";

interface EvalTableProps {
  run: EvalRun;
}

const MetricDecimals = 3;
const PercentDecimals = 1;

const ModeRows: { mode: RetrievalMode; label: string }[] = [
  { mode: "dense", label: "Meaning (dense)" },
  { mode: "sparse", label: "Keyword (sparse)" },
  { mode: "hybrid", label: "Combined (hybrid)" },
];

export function formatMetric(value: number): string {
  return value.toFixed(MetricDecimals);
}

/** Faithfulness as "verified / emitted (percent)", or a dash when no citations were emitted. */
export function formatFaithfulness(emitted: number, verified: number): string {
  if (emitted === 0) {
    return "no citations emitted";
  }
  const percent = ((verified / emitted) * 100).toFixed(PercentDecimals);
  return `${verified} / ${emitted} (${percent}%)`;
}

function renderModeRow(label: string, mode: RetrievalMode, metrics: ModeMetrics | null | undefined) {
  if (!metrics) {
    return (
      <tr key={mode} data-testid={`eval-row-${mode}`}>
        <th scope="row">{label}</th>
        <td colSpan={3} className="empty-state">
          skipped (embeddings unavailable)
        </td>
      </tr>
    );
  }
  return (
    <tr key={mode} data-testid={`eval-row-${mode}`}>
      <th scope="row">{label}</th>
      <td className="number">{formatMetric(metrics.recallAt5)}</td>
      <td className="number">{formatMetric(metrics.recallAt10)}</td>
      <td className="number">{formatMetric(metrics.mrr)}</td>
    </tr>
  );
}

export function EvalTable({ run }: EvalTableProps) {
  const faithfulness = run.metrics.faithfulness;
  return (
    <div data-testid="eval-table">
      <p className="meta">
        Run #{run.id} · {run.ranAt} · {run.caseCount} ground-truth cases
      </p>
      <table className="eval-table">
        <thead>
          <tr>
            <th scope="col">Mode</th>
            <th scope="col">Recall@5</th>
            <th scope="col">Recall@10</th>
            <th scope="col">MRR</th>
          </tr>
        </thead>
        <tbody>{ModeRows.map((row) => renderModeRow(row.label, row.mode, run.metrics[row.mode]))}</tbody>
      </table>
      <p data-testid="faithfulness">
        Citation faithfulness:{" "}
        {faithfulness ? formatFaithfulness(faithfulness.emitted, faithfulness.verified) : "not measured (answering unavailable)"}
      </p>
    </div>
  );
}
